using System.Globalization;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Common.Helpers;

public sealed partial class MathExpressionResolverService : IMathExpressionResolver
{
    // Matches bare identifiers: letter/underscore followed by word chars
    [GeneratedRegex(@"[A-Za-z_]\w*", RegexOptions.NonBacktracking)]
    private static partial Regex IdentifierPattern();

    public string Resolve(string expression, IReadOnlyDictionary<string, string?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        // Substitute known numeric variables; leave unknown tokens intact
        string substituted = IdentifierPattern().Replace(expression, m =>
        {
            string token = m.Value;
            if (variables.TryGetValue(token, out string? val)
                && !string.IsNullOrWhiteSpace(val)
                && decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                return $"({val})";
            }
            return token;
        });

        var parser = new ExpressionParser(substituted.AsSpan());
        if (parser.TryParse(out decimal dec))
        {
            // G10 trims trailing zeros (1.50 -> 1.5, 2.00 -> 2)
            return dec.ToString("G10", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private ref struct ExpressionParser(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> _text = text;
        private int _pos = 0;

        public bool TryParse(out decimal result)
        {
            result = 0m;
            SkipWhitespace();
            if (_pos >= _text.Length)
                return false;

            if (!TryParseExpression(out result))
                return false;

            SkipWhitespace();
            return _pos == _text.Length;
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }

        private bool TryParseExpression(out decimal result)
        {
            if (!TryParseTerm(out result))
                return false;

            while (true)
            {
                SkipWhitespace();
                if (_pos >= _text.Length)
                    break;

                char op = _text[_pos];
                if (op != '+' && op != '-')
                    break;

                _pos++;
                if (!TryParseTerm(out decimal right))
                    return false;

                try
                {
                    result = op switch
                    {
                        '+' => checked(result + right),
                        '-' => checked(result - right),
                        _ => result
                    };
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryParseTerm(out decimal result)
        {
            if (!TryParseFactor(out result))
                return false;

            while (true)
            {
                SkipWhitespace();
                if (_pos >= _text.Length)
                    break;

                char op = _text[_pos];
                if (op != '*' && op != '/' && op != '%')
                    break;

                _pos++;
                if (!TryParseFactor(out decimal right))
                    return false;

                try
                {
                    if (op == '/')
                    {
                        if (right == 0m)
                            return false;
                        result = result / right;
                    }
                    else if (op == '%')
                    {
                        if (right == 0m)
                            return false;
                        result = result % right;
                    }
                    else
                    {
                        result = checked(result * right);
                    }
                }
                catch (OverflowException)
                {
                    return false;
                }
                catch (DivideByZeroException)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryParseFactor(out decimal result)
        {
            SkipWhitespace();
            if (_pos >= _text.Length)
            {
                result = 0m;
                return false;
            }

            char c = _text[_pos];
            if (c == '+' || c == '-')
            {
                _pos++;
                if (!TryParseFactor(out decimal val))
                {
                    result = 0m;
                    return false;
                }

                try
                {
                    result = (c == '-') ? checked(-val) : val;
                    return true;
                }
                catch (OverflowException)
                {
                    result = 0m;
                    return false;
                }
            }

            return TryParsePrimary(out result);
        }

        private bool TryParsePrimary(out decimal result)
        {
            result = 0m;
            SkipWhitespace();
            if (_pos >= _text.Length)
                return false;

            if (_text[_pos] == '(')
            {
                _pos++;
                if (!TryParseExpression(out result))
                    return false;

                SkipWhitespace();
                if (_pos >= _text.Length || _text[_pos] != ')')
                    return false;

                _pos++;
                return true;
            }

            int start = _pos;
            bool hasDot = false;
            while (_pos < _text.Length)
            {
                char ch = _text[_pos];
                if (char.IsAsciiDigit(ch))
                {
                    _pos++;
                }
                else if (ch == '.' && !hasDot)
                {
                    hasDot = true;
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            if (start == _pos)
                return false;

            ReadOnlySpan<char> numSpan = _text[start.._pos];
            return decimal.TryParse(numSpan, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
        }
    }
}

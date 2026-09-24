namespace SmkDoc.Application.Common.Interfaces;

public interface IMathExpressionResolver
{
    /// <summary>
    /// Evaluates a math expression with named variable substitution.
    /// Returns string.Empty if the expression cannot be evaluated.
    /// </summary>
    string Resolve(string expression, IReadOnlyDictionary<string, string?> variables);
}

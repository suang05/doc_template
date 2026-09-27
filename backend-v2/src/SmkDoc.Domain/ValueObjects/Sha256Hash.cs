using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

public class Sha256Hash : ValueObject
{
    public string Value { get; }

    public Sha256Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Hash value cannot be null or empty.");

        if (value.Length != 64 || !IsValidHex(value))
            throw new ArgumentException("Hash must be exactly 64 hexadecimal characters.");

        Value = value.ToLowerInvariant();
    }

    private static bool IsValidHex(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}


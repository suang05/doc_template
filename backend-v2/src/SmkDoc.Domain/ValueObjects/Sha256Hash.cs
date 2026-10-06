using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated 64-character lowercase SHA-256 hexadecimal hash.
/// </summary>
public sealed class Sha256Hash : ValueObject
{
    public const int HashLength = 64;

    public string Value { get; }

    public Sha256Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Hash value cannot be null or empty.");
        }

        if (value.Length != HashLength || !IsValidHex(value))
        {
            throw new DomainValidationException($"Hash must be exactly {HashLength} hexadecimal characters.");
        }

        Value = value.ToLowerInvariant();
    }

    public static Sha256Hash Create(string value) => new(value);

    private static bool IsValidHex(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Sha256Hash hash) => hash.Value;
}

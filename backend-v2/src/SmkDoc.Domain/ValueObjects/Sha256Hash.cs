using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated 64-character lowercase SHA-256 hexadecimal hash.
/// </summary>
public sealed record class Sha256Hash
{
    public const int HashLength = 64;

    public string Value { get; }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Canonical lowercase SHA-256 hex digest normalization.")]
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

    private static bool IsValidHex(string s) => s.All(char.IsAsciiHexDigit);

    public override string ToString() => Value;

    public static implicit operator string(Sha256Hash hash) => hash.Value;
}

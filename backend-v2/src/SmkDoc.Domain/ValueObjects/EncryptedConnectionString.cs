using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing an encrypted connection string.
/// Enforces non-blank validation and redacts contents in ToString() to prevent credential exposure in logs.
/// </summary>
public sealed record class EncryptedConnectionString
{
    public string Value { get; }

    private EncryptedConnectionString(string value) => Value = value;

    public static EncryptedConnectionString Create(string? value) =>
        new(Guard.NotBlank(value, "Encrypted connection string"));

    public override string ToString() => "[REDACTED]";

    public static implicit operator string(EncryptedConnectionString connStr) => connStr.Value;
}

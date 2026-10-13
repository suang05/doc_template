using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Value Object representing a unique business document reference identifier (e.g. "INV-2026-0001", "SC-001").
/// </summary>
public sealed record class DocumentReference
{
    public const int MaxLength = 100;

    public string Value { get; }

    private DocumentReference(string value) => Value = value;

    public static DocumentReference Create(string? value) =>
        new(Guard.NotBlank(value, "Document reference", MaxLength));

    public override string ToString() => Value;

    public static implicit operator string(DocumentReference reference) => reference.Value;
    public static implicit operator DocumentReference(string value) => Create(value);
}

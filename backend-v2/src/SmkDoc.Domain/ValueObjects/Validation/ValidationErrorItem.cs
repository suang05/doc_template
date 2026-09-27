namespace SmkDoc.Domain.ValueObjects.Validation;

/// <summary>
/// Domain Value Object representing a single validation error in a schema contract evaluation.
/// Pure C# POCO with zero framework or external library dependencies.
/// </summary>
/// <param name="Field">JSON Pointer path (e.g., "/customer/tax_id").</param>
/// <param name="Rule">Schema rule or keyword violated (e.g., "minLength", "required", "type").</param>
/// <param name="Message">Human-readable description of the violation.</param>
public record ValidationErrorItem(
    string Field,
    string Rule,
    string Message
);

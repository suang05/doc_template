namespace SmkDoc.Domain.ValueObjects.Validation;

/// <summary>
/// Domain Model / Result Object representing the outcome of a schema validation run.
/// Pure C# POCO with zero framework or external library dependencies.
/// </summary>
public class SchemaValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationErrorItem> Errors { get; }

    private SchemaValidationResult(bool isValid, List<ValidationErrorItem> errors)
    {
        IsValid = isValid;
        Errors = errors.AsReadOnly();
    }

    public static SchemaValidationResult Success() =>
        new(true, []);

    public static SchemaValidationResult Failure(List<ValidationErrorItem> errors) =>
        new(false, errors);

    public static SchemaValidationResult Failure(params ValidationErrorItem[] errors) =>
        new(false, errors.ToList());
}

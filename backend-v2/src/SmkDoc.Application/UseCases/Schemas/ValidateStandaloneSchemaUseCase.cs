using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Schemas;

namespace SmkDoc.Application.UseCases.Schemas;

/// <summary>
/// Stateless, zero-database standalone schema validation use case.
/// Dry-runs a JSON payload against a Draft-07 JSON Schema.
/// Used by Monaco Editor Studio and external M2M systems.
/// </summary>
public sealed class ValidateStandaloneSchemaUseCase
{
    private readonly IJsonSchemaValidationService _schemaValidator;

    public ValidateStandaloneSchemaUseCase(IJsonSchemaValidationService schemaValidator)
    {
        _schemaValidator = schemaValidator;
    }

    public ValidateStandaloneSchemaResult Execute(ValidateStandaloneSchemaCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new ValidateStandaloneSchemaResult(
                Valid: false,
                Message: "Schema definition is missing or null."
            );
        }

        if (command.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new ValidateStandaloneSchemaResult(
                Valid: false,
                Message: "Payload data is missing or null."
            );
        }

        var result = _schemaValidator.Validate(command.Schema, command.Payload);

        if (!result.IsValid)
        {
            return new ValidateStandaloneSchemaResult(
                Valid: false,
                Message: "Payload does not conform to the provided JSON Schema.",
                Errors: result.Errors
            );
        }

        return new ValidateStandaloneSchemaResult(
            Valid: true,
            Message: "Schema validation passed successfully."
        );
    }
}

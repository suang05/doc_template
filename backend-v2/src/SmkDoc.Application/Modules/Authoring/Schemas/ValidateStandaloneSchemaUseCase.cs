using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Schemas.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Schemas;

/// <summary>
/// Stateless, zero-database standalone schema validation use case.
/// Dry-runs a JSON payload against a Draft-07 JSON Schema.
/// Used by Monaco Editor Studio and external M2M systems.
/// </summary>
public sealed class ValidateStandaloneSchemaUseCase(IJsonSchemaValidationService schemaValidator)
    : IUseCase<ValidateStandaloneSchemaCommand, ValidateStandaloneSchemaResult>
{
    public Task<ValidateStandaloneSchemaResult> ExecuteAsync(ValidateStandaloneSchemaCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Task.FromResult(new ValidateStandaloneSchemaResult(
                Valid: false,
                Message: "Schema definition is missing or null."
            ));
        }

        if (command.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Task.FromResult(new ValidateStandaloneSchemaResult(
                Valid: false,
                Message: "Payload data is missing or null."
            ));
        }

        var result = schemaValidator.Validate(command.Schema, command.Payload);

        if (!result.IsValid)
        {
            return Task.FromResult(new ValidateStandaloneSchemaResult(
                Valid: false,
                Message: "Payload does not conform to the provided JSON Schema.",
                Errors: result.Errors
            ));
        }

        return Task.FromResult(new ValidateStandaloneSchemaResult(
            Valid: true,
            Message: "Schema validation passed successfully."
        ));
    }
}

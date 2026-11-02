using System.Text.Json;

namespace SmkDoc.Application.Modules.Authoring.Schemas.Commands.ValidateStandaloneSchema;

/// <summary>
/// Command contract for validating an arbitrary payload against an arbitrary JSON Schema Draft-07.
/// </summary>
public sealed record ValidateStandaloneSchemaCommand(
    JsonElement Schema,
    JsonElement Payload
);

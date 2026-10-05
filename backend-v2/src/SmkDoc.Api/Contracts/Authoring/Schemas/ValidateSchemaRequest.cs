using System.Text.Json;

namespace SmkDoc.Api.Contracts.Authoring.Schemas;

public record ValidateSchemaRequest(JsonElement Schema, JsonElement Payload);

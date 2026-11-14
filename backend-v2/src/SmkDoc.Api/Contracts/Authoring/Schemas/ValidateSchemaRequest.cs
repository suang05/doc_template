using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.Authoring.Schemas;

public record ValidateSchemaRequest([property: JsonRequired] JsonElement Schema, [property: JsonRequired] JsonElement Payload);

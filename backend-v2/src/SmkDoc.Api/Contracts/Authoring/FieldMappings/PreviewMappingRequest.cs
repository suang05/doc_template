using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.Authoring.FieldMappings;

public record PreviewMappingRequest([property: JsonRequired] JsonElement SampleData);

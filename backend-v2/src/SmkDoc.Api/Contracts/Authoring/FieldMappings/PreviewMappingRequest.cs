using System.Text.Json;

namespace SmkDoc.Api.Contracts.Authoring.FieldMappings;

public record PreviewMappingRequest(JsonElement SampleData);

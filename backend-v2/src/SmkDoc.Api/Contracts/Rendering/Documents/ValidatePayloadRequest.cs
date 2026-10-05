using System.Text.Json;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

public record ValidatePayloadRequest(JsonElement Data);

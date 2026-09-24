using System.Text.Json;

namespace SmkDoc.Application.Common.Models;

public record PreviewDocumentRequest(
    JsonElement Data,
    string? Html = null
);

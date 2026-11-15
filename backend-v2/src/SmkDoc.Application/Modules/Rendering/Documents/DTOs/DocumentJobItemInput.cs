using System.Text.Json;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

public sealed record DocumentJobItemInput(
    JsonElement Payload,
    string? DocumentRef = null);

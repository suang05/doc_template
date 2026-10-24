using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Domain parameter record encapsulating caller identification and source context for document generation.
/// </summary>
public sealed record GenerationContext(
    string? CallerApp = null,
    string? TriggerSource = "api",
    ApiKeyId? ApiKeyId = null);

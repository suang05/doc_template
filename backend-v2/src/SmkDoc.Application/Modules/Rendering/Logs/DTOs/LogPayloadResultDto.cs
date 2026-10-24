namespace SmkDoc.Application.Modules.Rendering.Logs.DTOs;

/// <summary>
/// Resolved payload result for an audit log entry.
/// </summary>
public sealed record class LogPayloadResultDto(
    Guid LogId,
    string? PayloadJson,
    bool IsOffloaded,
    string? OffloadedKey);

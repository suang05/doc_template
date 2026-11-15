namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Exposes the authoritative monotonic optimistic concurrency version of a mutable resource DTO.
/// Drives strong HTTP ETag headers (<c>"v{Version}"</c>).
/// </summary>
public interface IHasVersion
{
    long Version { get; }
}

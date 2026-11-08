namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Implemented by Application Result DTOs that expose a concurrency token to be emitted as an HTTP ETag header.
/// </summary>
public interface IHasConcurrencyToken
{
    string? ConcurrencyToken { get; }
}


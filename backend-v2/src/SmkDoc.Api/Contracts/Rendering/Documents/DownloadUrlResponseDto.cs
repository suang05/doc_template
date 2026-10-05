namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// Response payload containing pre-signed document download URL and its validity window.
/// </summary>
public sealed record DownloadUrlResponseDto(string Url, int ExpiresInSeconds = 3600);

namespace SmkDoc.Api.Contracts.Integration.DataConnections;

/// <summary>
/// Response payload for database connection connectivity test.
/// </summary>
public sealed record TestDataConnectionResponseDto(bool Success, string? Message = null, string? Error = null);

namespace SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

/// <summary>
/// Result DTO representing a safe database connection definition (connection string omitted).
/// </summary>
public sealed record DataConnectionResultDto(
    Guid Id,
    string Name,
    string Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyToken = null);

/// <summary>
/// Result returned after testing connectivity to an external database.
/// </summary>
public sealed record TestDataConnectionResultDto(
    bool Success,
    string? Message = null,
    string? Error = null);

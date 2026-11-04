namespace SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

/// <summary>
/// DTO representing a safe database connection definition (connection string omitted).
/// </summary>
public sealed record DataConnectionDto(
    Guid Id,
    string Name,
    string Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Result returned after testing connectivity to an external database.
/// </summary>
public sealed record TestDataConnectionResultDto(
    bool Success,
    string? Message = null,
    string? Error = null);

namespace SmkDoc.Api.Contracts.Common;

/// <summary>
/// Root operational metadata for the document generation microservice.
/// </summary>
public sealed record ServerStatusResponse(
    string Service,
    string Architecture,
    string Status,
    string Version,
    string Docs,
    string Openapi);

/// <summary>
/// Health check report payload summarizing service and dependency liveness.
/// </summary>
public sealed record HealthCheckResponse(
    string Status,
    IEnumerable<HealthCheckItem> Checks,
    int TotalDurationMs);

/// <summary>
/// Individual dependency health check report item.
/// </summary>
public sealed record HealthCheckItem(
    string Name,
    string Status,
    string? Description,
    int DurationMs);

namespace SmkDoc.Api.Models;

/// <summary>
/// Standard API Response wrapper for successful requests that return data.
/// Enforces the "Envelope Pattern" for consistent JSON schemas.
/// </summary>
public record ApiResponse<T>(T Data);

/// <summary>
/// Paged API Response wrapper for list endpoints.
/// </summary>
public record PagedApiResponse<T>(
    IEnumerable<T> Data,
    int Total,
    int Page,
    int Limit
) : ApiResponse<IEnumerable<T>>(Data);

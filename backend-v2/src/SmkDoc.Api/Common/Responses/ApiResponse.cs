namespace SmkDoc.Api.Common.Responses;

/// <summary>
/// Standard API Response wrapper for successful requests that return data.
/// Enforces the "Envelope Pattern" for consistent JSON schemas.
/// </summary>
public record ApiResponse<T>(T Data);

/// <summary>
/// Standard pagination telemetry metadata envelope conforming to global REST API standards.
/// </summary>
public record PaginationMetadata(
    int Page,
    int Limit,
    int TotalCount,
    int TotalPages
)
{
    public static PaginationMetadata Create(int totalCount, int page, int limit)
    {
        var safeLimit = Math.Max(1, limit);
        var safePage = Math.Max(1, page);
        var safeTotal = Math.Max(0, totalCount);
        var totalPages = (int)Math.Ceiling((double)safeTotal / safeLimit);

        return new PaginationMetadata(safePage, safeLimit, safeTotal, totalPages);
    }
}

/// <summary>
/// Paged API Response wrapper for collection endpoints.
/// Encapsulates resource items in 'data' and pagination metadata in 'pagination'.
/// </summary>
public record PagedApiResponse<T>(
    IEnumerable<T> Data,
    PaginationMetadata Pagination
) : ApiResponse<IEnumerable<T>>(Data)
{
    public PagedApiResponse(IEnumerable<T> data, int totalCount, int page, int limit)
        : this(data, PaginationMetadata.Create(totalCount, page, limit))
    {
    }
}

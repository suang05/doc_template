using System.Text.Json.Serialization;

namespace SmkDoc.Api.Common.Responses;

/// <summary>
/// Non-generic envelope contract enabling zero-reflection payload inspection in global filters.
/// </summary>
public interface IApiResponse
{
    object? Data { get; }
}

/// <summary>
/// Standard API Response wrapper for successful requests that return data.
/// Enforces the "Envelope Pattern" for consistent JSON schemas.
/// </summary>
public record ApiResponse<T>(T Data) : IApiResponse
{
    object? IApiResponse.Data => Data;
}

/// <summary>
/// Standard pagination telemetry metadata envelope conforming to global REST API standards.
/// </summary>
public sealed record PaginationMetadata(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Page,
    int Limit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? TotalCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TotalPages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor = null
)
{
    public static PaginationMetadata ForOffset(int page, int limit, long? totalCount)
    {
        var safeLimit = Math.Max(1, limit);
        var safePage = Math.Max(1, page);
        long? safeTotal = totalCount is null ? null : Math.Max(0L, totalCount.Value);
        int? totalPages = safeTotal switch
        {
            null => null,
            <= 0L => 0,
            _ => (int)((safeTotal.Value + safeLimit - 1) / safeLimit)
        };

        return new PaginationMetadata(safePage, safeLimit, safeTotal, totalPages, null);
    }

    public static PaginationMetadata ForCursor(int limit, string? nextCursor) =>
        new(null, Math.Max(1, limit), null, null, string.IsNullOrWhiteSpace(nextCursor) ? null : nextCursor);
}

/// <summary>
/// Paged API Response wrapper for collection endpoints.
/// Encapsulates resource items in 'data' and pagination metadata in 'pagination'.
/// </summary>
public record PagedApiResponse<T>(
    IReadOnlyList<T> Data,
    PaginationMetadata Pagination
) : ApiResponse<IReadOnlyList<T>>(Data)
{
    public PagedApiResponse(IReadOnlyList<T> data, long? totalCount, int page, int limit)
        : this(data, PaginationMetadata.ForOffset(page, limit, totalCount))
    {
    }
}

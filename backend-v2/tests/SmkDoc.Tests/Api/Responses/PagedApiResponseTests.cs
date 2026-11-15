using System.Text.Json;
using SmkDoc.Api.Common.Responses;

namespace SmkDoc.Tests.Api.Responses;

public sealed class PagedApiResponseTests
{
    private static readonly JsonSerializerOptions CamelCaseIndentedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private sealed record SampleItem(string Id, string Name, string Slug);

    [Theory]
    [InlineData(42L, 1, 20, 3)]
    [InlineData(40L, 1, 20, 2)]
    [InlineData(0L, 1, 20, 0)]
    [InlineData(1L, 1, 20, 1)]
    [InlineData(100L, 5, 20, 5)]
    public void PaginationMetadata_ForOffset_CalculatesTotalPagesCorrectly(
        long totalCount,
        int page,
        int limit,
        int expectedTotalPages)
    {
        // Act
        var metadata = PaginationMetadata.ForOffset(page, limit, totalCount);

        // Assert
        Assert.Equal(page, metadata.Page);
        Assert.Equal(limit, metadata.Limit);
        Assert.Equal(totalCount, metadata.TotalCount);
        Assert.Equal(expectedTotalPages, metadata.TotalPages);
        Assert.Null(metadata.NextCursor);
    }

    [Fact]
    public void PaginationMetadata_ForOffset_DefensivelyHandlesZeroOrNegativeInputs()
    {
        // Act
        var metadata = PaginationMetadata.ForOffset(page: 0, limit: 0, totalCount: -10);

        // Assert: Limit clamped to 1 to prevent division by zero; Page clamped to 1; Total clamped to 0
        Assert.Equal(1, metadata.Page);
        Assert.Equal(1, metadata.Limit);
        Assert.Equal(0L, metadata.TotalCount);
        Assert.Equal(0, metadata.TotalPages);
    }

    [Fact]
    public void PaginationMetadata_ForOffset_WhenTotalCountNull_OmitsTotalFieldsFromJson()
    {
        // Act
        var pagedResponse = new PagedApiResponse<SampleItem>([], totalCount: null, page: 2, limit: 25);
        var json = JsonSerializer.Serialize(pagedResponse, CamelCaseIndentedJsonOptions);
        using var document = JsonDocument.Parse(json);
        var paginationProp = document.RootElement.GetProperty("pagination");

        // Assert
        Assert.Equal(2, paginationProp.GetProperty("page").GetInt32());
        Assert.Equal(25, paginationProp.GetProperty("limit").GetInt32());
        Assert.False(paginationProp.TryGetProperty("totalCount", out _));
        Assert.False(paginationProp.TryGetProperty("totalPages", out _));
        Assert.False(paginationProp.TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public void PaginationMetadata_ForCursor_OmitsOffsetFieldsAndSerializesCursorWhenPresent()
    {
        // Act
        var withCursor = new PagedApiResponse<SampleItem>([], PaginationMetadata.ForCursor(20, "cursor-abc"));
        var withCursorJson = JsonSerializer.Serialize(withCursor, CamelCaseIndentedJsonOptions);
        using var withCursorDoc = JsonDocument.Parse(withCursorJson);
        var withCursorPagination = withCursorDoc.RootElement.GetProperty("pagination");

        var endOfStream = new PagedApiResponse<SampleItem>([], PaginationMetadata.ForCursor(20, " "));
        var endJson = JsonSerializer.Serialize(endOfStream, CamelCaseIndentedJsonOptions);
        using var endDoc = JsonDocument.Parse(endJson);
        var endPagination = endDoc.RootElement.GetProperty("pagination");

        // Assert
        Assert.Equal(20, withCursorPagination.GetProperty("limit").GetInt32());
        Assert.Equal("cursor-abc", withCursorPagination.GetProperty("nextCursor").GetString());
        Assert.False(withCursorPagination.TryGetProperty("page", out _));
        Assert.False(withCursorPagination.TryGetProperty("totalCount", out _));
        Assert.False(withCursorPagination.TryGetProperty("totalPages", out _));

        Assert.False(endPagination.TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public void PagedApiResponse_Serialization_MatchesInternationalStandardContract()
    {
        // Arrange
        var items = new List<SampleItem>
        {
            new("01926b42-7c3a-7000-8000-123456789abc", "Commercial Tax Invoice", "tax-invoice-th")
        };
        var pagedResponse = new PagedApiResponse<SampleItem>(items, totalCount: 42L, page: 1, limit: 20);

        // Act
        var json = JsonSerializer.Serialize(pagedResponse, CamelCaseIndentedJsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("data", out var dataProp));
        Assert.Equal(JsonValueKind.Array, dataProp.ValueKind);
        Assert.Equal(1, dataProp.GetArrayLength());

        var firstItem = dataProp[0];
        Assert.Equal("01926b42-7c3a-7000-8000-123456789abc", firstItem.GetProperty("id").GetString());
        Assert.Equal("Commercial Tax Invoice", firstItem.GetProperty("name").GetString());
        Assert.Equal("tax-invoice-th", firstItem.GetProperty("slug").GetString());

        Assert.True(root.TryGetProperty("pagination", out var paginationProp));
        Assert.Equal(1, paginationProp.GetProperty("page").GetInt32());
        Assert.Equal(20, paginationProp.GetProperty("limit").GetInt32());
        Assert.Equal(42, paginationProp.GetProperty("totalCount").GetInt64());
        Assert.Equal(3, paginationProp.GetProperty("totalPages").GetInt32());
        Assert.False(paginationProp.TryGetProperty("nextCursor", out _));
    }
}

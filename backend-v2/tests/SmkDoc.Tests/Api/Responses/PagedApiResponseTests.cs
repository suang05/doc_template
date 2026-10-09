using System.Text.Json;
using SmkDoc.Api.Common.Responses;

namespace SmkDoc.Tests.Api.Responses;

public class PagedApiResponseTests
{
    private record SampleItem(string Id, string Name, string Slug);

    [Theory]
    [InlineData(42, 1, 20, 3)]
    [InlineData(40, 1, 20, 2)]
    [InlineData(0, 1, 20, 0)]
    [InlineData(1, 1, 20, 1)]
    [InlineData(100, 5, 20, 5)]
    public void PaginationMetadata_Create_CalculatesTotalPagesCorrectly(
        int totalCount,
        int page,
        int limit,
        int expectedTotalPages)
    {
        // Act
        var metadata = PaginationMetadata.Create(totalCount, page, limit);

        // Assert
        Assert.Equal(page, metadata.Page);
        Assert.Equal(limit, metadata.Limit);
        Assert.Equal(totalCount, metadata.TotalCount);
        Assert.Equal(expectedTotalPages, metadata.TotalPages);
    }

    [Fact]
    public void PaginationMetadata_Create_DefensivelyHandlesZeroOrNegativeInputs()
    {
        // Act
        var metadata = PaginationMetadata.Create(totalCount: -10, page: 0, limit: 0);

        // Assert: Limit clamped to 1 to prevent division by zero; Page clamped to 1; Total clamped to 0
        Assert.Equal(1, metadata.Page);
        Assert.Equal(1, metadata.Limit);
        Assert.Equal(0, metadata.TotalCount);
        Assert.Equal(0, metadata.TotalPages);
    }

    [Fact]
    public void PagedApiResponse_Serialization_MatchesInternationalStandardContract()
    {
        // Arrange
        var items = new List<SampleItem>
        {
            new("01926b42-7c3a-7000-8000-123456789abc", "Commercial Tax Invoice", "tax-invoice-th")
        };
        var pagedResponse = new PagedApiResponse<SampleItem>(items, totalCount: 42, page: 1, limit: 20);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Act
        var json = JsonSerializer.Serialize(pagedResponse, jsonOptions);
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
        Assert.Equal(42, paginationProp.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, paginationProp.GetProperty("totalPages").GetInt32());
    }
}

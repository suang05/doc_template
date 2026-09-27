using FluentAssertions;
using SmkDoc.Infrastructure.Parsing;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Parsing;

public class JsonDataParserTests
{
    private readonly JsonDataParser _parser = new();

    [Fact]
    public void ToHierarchy_ShouldParseNestedObjectsAndArrays()
    {
        string json = @"{
            ""title"": ""สัญญาจะซื้อจะขาย"",
            ""buyer"": {
                ""name"": ""นายสมชาย ใจดี"",
                ""age"": 35
            },
            ""units"": [
                { ""unitNo"": ""A101"", ""price"": 2500000 },
                { ""unitNo"": ""A102"", ""price"": 3200000 }
            ]
        }";

        var dict = _parser.ToHierarchy(json);

        dict.Should().ContainKey("title");
        dict["title"].Should().Be("สัญญาจะซื้อจะขาย");
        
        dict.Should().ContainKey("buyer");
        var buyer = dict["buyer"] as Dictionary<string, object>;
        buyer.Should().NotBeNull();
        buyer!["name"].Should().Be("นายสมชาย ใจดี");

        dict.Should().ContainKey("units");
        var units = dict["units"] as List<object>;
        units.Should().NotBeNull();
        units.Should().HaveCount(2);
    }

    [Fact]
    public void Flatten_ShouldProduceReplacementsAndTables_ForWord()
    {
        string json = @"{
            ""projectName"": ""สัมมากร ชัยพฤกษ์"",
            ""customer"": {
                ""name"": ""คุณวิภา""
            },
            ""items"": [
                { ""code"": ""001"", ""desc"": ""เงินจอง"" },
                { ""code"": ""002"", ""desc"": ""เงินทำสัญญา"" }
            ]
        }";

        var (replacements, tables) = _parser.Flatten(json);

        replacements.Should().ContainKey("projectName");
        replacements["projectName"].Should().Be("สัมมากร ชัยพฤกษ์");
        replacements.Should().ContainKey("customer.name");
        replacements["customer.name"].Should().Be("คุณวิภา");

        tables.Should().HaveCount(1);
        tables[0].Should().HaveCount(2);
        tables[0][0]["code"].Should().Be("001");
        tables[0][1]["desc"].Should().Be("เงินทำสัญญา");
    }

    [Fact]
    public void FlattenNamed_ShouldProduceNamedArrays_ForExcel()
    {
        string json = @"{
            ""docNo"": ""DOC-999"",
            ""payments"": [
                { ""no"": ""1"", ""amount"": ""50000"" }
            ]
        }";

        var (replacements, arrays) = _parser.FlattenNamed(json);

        replacements["docNo"].Should().Be("DOC-999");
        arrays.Should().ContainKey("payments");
        arrays["payments"].Should().HaveCount(1);
    }
}

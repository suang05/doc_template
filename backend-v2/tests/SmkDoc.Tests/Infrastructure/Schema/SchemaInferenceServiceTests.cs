using FluentAssertions;
using SmkDoc.Infrastructure.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Schema;

public class SchemaInferenceServiceTests
{
    private readonly SchemaInferenceService _service = new();

    [Fact]
    public void InferSchemaFromPlaceholders_WithVariousTypes_ShouldGenerateValidDraft07JsonSchema()
    {
        // Arrange
        var placeholders = new[]
        {
            "customer_name",
            "grand_total:number",
            "contract_date:thai_date",
            "tracking_url:qr",
            "items.name",
            "items.price:number",
            "items.qty:number"
        };

        // Act
        string schemaJson = _service.InferSchemaFromPlaceholders(placeholders);

        // Assert
        schemaJson.Should().NotBeNullOrWhiteSpace();
        var schema = JsonNode.Parse(schemaJson)?.AsObject();
        schema.Should().NotBeNull();

        schema!["$schema"]?.GetValue<string>().Should().Be("http://json-schema.org/draft-07/schema#");
        schema["type"]?.GetValue<string>().Should().Be("object");

        var props = schema["properties"]?.AsObject();
        props.Should().NotBeNull();
        props!["customer_name"]?["type"]?.GetValue<string>().Should().Be("string");
        props["grand_total"]?["type"]?.GetValue<string>().Should().Be("number");
        props["contract_date"]?["type"]?.GetValue<string>().Should().Be("string");
        props["contract_date"]?["format"]?.GetValue<string>().Should().Be("date");
        props["tracking_url"]?["type"]?.GetValue<string>().Should().Be("string");

        // Verify array
        props["items"]?["type"]?.GetValue<string>().Should().Be("array");
        var itemProps = props["items"]?["items"]?["properties"]?.AsObject();
        itemProps.Should().NotBeNull();
        itemProps!["name"]?["type"]?.GetValue<string>().Should().Be("string");
        itemProps["price"]?["type"]?.GetValue<string>().Should().Be("number");
    }

    [Fact]
    public void GenerateDefaultSamplePayload_ShouldProduceValidJsonWithRealisticValues()
    {
        // Arrange
        var placeholders = new[]
        {
            "customer_name",
            "grand_total:number",
            "contract_date:thai_date",
            "items.no",
            "items.name",
            "items.price:number"
        };

        // Act
        string payloadJson = _service.GenerateDefaultSamplePayload(placeholders);

        // Assert
        var root = JsonNode.Parse(payloadJson)?.AsObject();
        root.Should().NotBeNull();
        root!["customer_name"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        root["grand_total"]?.GetValue<decimal>().Should().Be(250000.00m);
        root["contract_date"]?.GetValue<string>().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");

        var items = root["items"]?.AsArray();
        items.Should().NotBeNull();
        items!.Count.Should().Be(2);
        items[0]?["no"]?.GetValue<string>().Should().Be("1");
        items[1]?["no"]?.GetValue<string>().Should().Be("2");
        items[0]?["price"]?.GetValue<decimal>().Should().BeGreaterThan(0);
    }

    [Fact]
    public void InferFromHtml_WithHandlebarsEachAndPlainVariables_ShouldInferBothSchemaAndPayload()
    {
        // Arrange
        string html = @"
<!DOCTYPE html>
<html>
<body>
  <h1>สัญญาเลขที่ {{contractNo}}</h1>
  <p>เรียน {{customerName}} ยอดรวม {{totalAmount:thai_baht_text}} บาท วันที่ {{signedDate:thai_date}}</p>
  <table>
    {{#each products}}
      <tr>
        <td>{{addOne @index}}</td>
        <td>{{title}}</td>
        <td>{{price:number}}</td>
      </tr>
    {{/each}}
  </table>
</body>
</html>";

        // Act
        var (schemaJson, samplePayloadJson) = _service.InferFromHtml(html);

        // Assert
        schemaJson.Should().NotBeNullOrWhiteSpace();
        samplePayloadJson.Should().NotBeNullOrWhiteSpace();

        var schema = JsonNode.Parse(schemaJson)?.AsObject();
        var props = schema!["properties"]?.AsObject();
        props.Should().ContainKey("contractNo");
        props.Should().ContainKey("customerName");
        props.Should().ContainKey("totalAmount");
        props.Should().ContainKey("signedDate");
        props.Should().ContainKey("products");

        props!["products"]?["type"]?.GetValue<string>().Should().Be("array");

        var payload = JsonNode.Parse(samplePayloadJson)?.AsObject();
        payload.Should().ContainKey("contractNo");
        payload.Should().ContainKey("customerName");
        payload.Should().ContainKey("totalAmount");
        payload.Should().ContainKey("signedDate");
        payload.Should().ContainKey("products");
    }

    [Fact]
    public void InferSchemaFromPlaceholders_WithStrictDraft07Rules_ShouldDistinguishSingleObjectAndArrayAndIncludeConstraints()
    {
        // Arrange
        var placeholders = new[]
        {
            "doc_no",
            "issue_date",
            "customer.name",
            "customer.tax_id",
            "customer.email",
            "items.name",
            "items.qty",
            "items.price"
        };

        // Act
        string schemaJson = _service.InferSchemaFromPlaceholders(placeholders, templateSlug: "tax-invoice");

        // Assert
        schemaJson.Should().NotBeNullOrWhiteSpace();
        var schema = JsonNode.Parse(schemaJson)?.AsObject();
        schema.Should().NotBeNull();

        // 1. Metadata check
        schema!["$schema"]?.GetValue<string>().Should().Be("http://json-schema.org/draft-07/schema#");
        schema["$id"]?.GetValue<string>().Should().Be("https://api.sammakorn.co.th/schemas/tax-invoice.json");
        schema["title"]?.GetValue<string>().Should().Be("TaxInvoiceDataContract");
        schema["additionalProperties"]?.GetValue<bool>().Should().BeTrue();

        var props = schema["properties"]?.AsObject();
        props.Should().NotBeNull();

        // 2. Scalar properties check
        props!["doc_no"]?["type"]?.GetValue<string>().Should().Be("string");
        props["doc_no"]?["minLength"]?.GetValue<int>().Should().Be(1);
        props["issue_date"]?["format"]?.GetValue<string>().Should().Be("date");

        // 3. Customer Single Object check (type: object, NOT array!)
        props["customer"]?["type"]?.GetValue<string>().Should().Be("object");
        props["customer"]?["additionalProperties"]?.GetValue<bool>().Should().BeFalse();
        var customerProps = props["customer"]?["properties"]?.AsObject();
        customerProps.Should().NotBeNull();
        customerProps!["name"]?["type"]?.GetValue<string>().Should().Be("string");
        customerProps["tax_id"]?["pattern"]?.GetValue<string>().Should().Be("^[0-9]{13}$");
        customerProps["email"]?["format"]?.GetValue<string>().Should().Be("email");

        // 4. Items Array check (type: array, minItems: 1)
        props["items"]?["type"]?.GetValue<string>().Should().Be("array");
        props["items"]?["minItems"]?.GetValue<int>().Should().Be(1);
        var itemProps = props["items"]?["items"]?["properties"]?.AsObject();
        itemProps.Should().NotBeNull();
        itemProps!["qty"]?["type"]?.GetValue<string>().Should().Be("integer");
        itemProps["qty"]?["minimum"]?.GetValue<int>().Should().Be(1);
        itemProps["price"]?["type"]?.GetValue<string>().Should().Be("number");
        itemProps["price"]?["minimum"]?.GetValue<decimal>().Should().Be(0);

        // 5. Sample payload check
        string sampleJson = _service.GenerateDefaultSamplePayload(placeholders);
        var sampleObj = JsonNode.Parse(sampleJson)?.AsObject();
        sampleObj.Should().NotBeNull();
        sampleObj!["customer"]?["tax_id"]?.GetValue<string>().Should().MatchRegex(@"^[0-9]{13}$");
        sampleObj["customer"]?["email"]?.GetValue<string>().Should().Contain("@");
        sampleObj["items"]?.AsArray().Should().NotBeNull();
    }
}

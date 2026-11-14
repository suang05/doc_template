using SmkDoc.Infrastructure.Engines.Placeholders;

namespace SmkDoc.Tests.Infrastructure.Engines.Placeholders;

public sealed class HandlebarsPlaceholderScannerTests
{
    [Fact]
    public void Scan_WithFlatVariablesAndObjectPaths_ShouldCategorizeScalarAndObject()
    {
        string template = """
            <h1>{{documentTitle}}</h1>
            <p>Customer: {{customer.name}}</p>
            <p>City: {{customer.address.city}}</p>
            <p>Total: {{totalAmount}}</p>
            """;

        var placeholders = HandlebarsPlaceholderScanner.Scan(template);

        placeholders.Should().Contain(p => p.Name == "documentTitle" && p.Category == PlaceholderCategory.Scalar);
        placeholders.Should().Contain(p => p.Name == "totalAmount" && p.Category == PlaceholderCategory.Scalar);
        placeholders.Should().Contain(p => p.Name == "customer.name" && p.Category == PlaceholderCategory.Object);
        placeholders.Should().Contain(p => p.Name == "customer.address.city" && p.Category == PlaceholderCategory.Object);
    }

    [Fact]
    public void Scan_WithEachBlock_ShouldCategorizeItemsAsTabularWithParentCollection()
    {
        string template = """
            <table>
              {{#each items}}
              <tr>
                <td>{{@index}}</td>
                <td>{{productName}}</td>
                <td>{{formatCurrency price}}</td>
                <td>{{../invoiceNo}}</td>
              </tr>
              {{/each}}
            </table>
            """;

        var placeholders = HandlebarsPlaceholderScanner.Scan(template);

        // Collection itself
        placeholders.Should().Contain(p => p.Name == "items" && p.Category == PlaceholderCategory.Tabular && p.DataType == "array");

        // Tabular items inside block
        placeholders.Should().Contain(p => p.Name == "productName" && p.Category == PlaceholderCategory.Tabular && p.ParentCollection == "items");
        placeholders.Should().Contain(p => p.Name == "price" && p.Category == PlaceholderCategory.Tabular && p.ParentCollection == "items" && p.DataType == "number");

        // System variable inside block
        placeholders.Should().Contain(p => p.Name == "@index" && p.Category == PlaceholderCategory.System);

        // Parent traversal
        placeholders.Should().Contain(p => p.Name == "invoiceNo" && p.Category == PlaceholderCategory.Scalar);
    }

    [Fact]
    public void Scan_WithHelpers_ShouldInferCorrectDataTypes()
    {
        string template = """
            <p>Date: {{formatDate createdDate "dd/MM/yyyy"}}</p>
            <p>Thai Date: {{thaidate approvedAt}}</p>
            <p>Currency: {{formatCurrency total}}</p>
            <p>Baht Text: {{bahttext total}}</p>
            """;

        var placeholders = HandlebarsPlaceholderScanner.Scan(template);

        placeholders.Should().Contain(p => p.Name == "createdDate" && p.DataType == "date");
        placeholders.Should().Contain(p => p.Name == "approvedAt" && p.DataType == "date");
        placeholders.Should().Contain(p => p.Name == "total" && p.DataType == "number");
    }

    [Fact]
    public void Scan_WithConditionalsAndComments_ShouldExtractConditionAndIgnoreComments()
    {
        string template = """
            {{!-- Comment block that should be ignored --}}
            {{#if isApproved}}
              <span>Approved</span>
            {{else}}
              <span>Pending</span>
            {{/if}}
            {{#unless hasDebt}}
              <span>Clear</span>
            {{/unless}}
            """;

        var placeholders = HandlebarsPlaceholderScanner.Scan(template);

        placeholders.Should().Contain(p => p.Name == "isApproved" && p.Category == PlaceholderCategory.Scalar && p.DataType == "boolean");
        placeholders.Should().Contain(p => p.Name == "hasDebt" && p.Category == PlaceholderCategory.Scalar && p.DataType == "boolean");
        placeholders.Should().NotContain(p => p.Name.Contains("Comment"));
        placeholders.Should().NotContain(p => p.Name == "else");
    }
}


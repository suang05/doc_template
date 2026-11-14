using System.Text;
using SmkDoc.Infrastructure.Engines;

namespace SmkDoc.Tests.Infrastructure.Parsing;

public sealed class TemplateScannerServiceTests
{
    [Fact]
    public async Task ScanPlaceholdersAsync_ShouldFilterHandlebarsBlocks_AndExtractValidPlaceholders()
    {
        var scanner = new TemplateScannerService();

        string html = @"
            <html>
            <body>
                <h1>{{companyName}}</h1>
                <div>{{customer.fullName}}</div>
                <div>{{amount:number}}</div>
                <div>{{total:thai_baht_text}}</div>
                <div>{{qr:trackingUrl}}</div>
                
                {{#each items}}
                    <div>Item {{addOne @index}}: {{name}} - {{price}}</div>
                {{/each}}

                {{#ifEquals status 'ACTIVE'}}
                    <span>Active User</span>
                {{else}}
                    <span>Inactive</span>
                {{/ifEquals}}

                {{#if hasVat}}
                    <span>VAT Included</span>
                {{/if}}
            </body>
            </html>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        var placeholders = await scanner.ScanPlaceholdersAsync(stream, ".html", CancellationToken.None);

        placeholders.Should().Contain("companyName");
        placeholders.Should().Contain("customer.fullName");
        placeholders.Should().Contain("amount:number");
        placeholders.Should().Contain("total:thai_baht_text");
        placeholders.Should().Contain("qr:trackingUrl");
        placeholders.Should().Contain("name");
        placeholders.Should().Contain("price");

        // Should NOT contain Handlebars block helpers, control structures, or subexpressions
        placeholders.Should().NotContain("#each items");
        placeholders.Should().NotContain("/each");
        placeholders.Should().NotContain("#ifEquals status 'ACTIVE'");
        placeholders.Should().NotContain("/ifEquals");
        placeholders.Should().NotContain("#if hasVat");
        placeholders.Should().NotContain("/if");
        placeholders.Should().NotContain("else");
        placeholders.Should().NotContain("@index");
        placeholders.Should().NotContain("addOne @index");
    }

    [Fact]
    public async Task ScanDetailedPlaceholdersAsync_ShouldReturnCategorizedPlaceholders()
    {
        var scanner = new TemplateScannerService();

        string html = @"
            <html>
            <body>
                <h1>{{companyName}}</h1>
                <div>{{customer.fullName}}</div>
                {{#each items}}
                    <div>{{@index}}: {{productName}} - {{formatCurrency price}}</div>
                {{/each}}
                {{#if hasDiscount}}
                    <div>Discount Applied</div>
                {{/if}}
            </body>
            </html>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        var detailed = await scanner.ScanDetailedPlaceholdersAsync(stream, ".html", CancellationToken.None);

        detailed.Should().Contain(p => p.Name == "companyName" && p.Category == PlaceholderCategory.Scalar);
        detailed.Should().Contain(p => p.Name == "customer.fullName" && p.Category == PlaceholderCategory.Object);
        detailed.Should().Contain(p => p.Name == "items" && p.Category == PlaceholderCategory.Tabular && p.DataType == "array");
        detailed.Should().Contain(p => p.Name == "productName" && p.Category == PlaceholderCategory.Tabular && p.ParentCollection == "items");
        detailed.Should().Contain(p => p.Name == "price" && p.Category == PlaceholderCategory.Tabular && p.ParentCollection == "items" && p.DataType == "number");
        detailed.Should().Contain(p => p.Name == "@index" && p.Category == PlaceholderCategory.System);
        detailed.Should().Contain(p => p.Name == "hasDiscount" && p.Category == PlaceholderCategory.Scalar && p.DataType == "boolean");
    }
}

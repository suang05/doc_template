using System.Text;
using FluentAssertions;
using SmkDoc.Infrastructure.Engines;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Parsing;

public class TemplateScannerServiceTests
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
        var placeholders = await scanner.ScanPlaceholdersAsync(stream, ".html");

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
}

using SmkDoc.Application.Common.Helpers;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Tests.Application.Common.Helpers;

public class PlaceholderHelperTests
{
    [Theory]
    // Text
    [InlineData("customerName", PlaceholderKind.Text, "customerName", null)]
    [InlineData("  orderTotal  ", PlaceholderKind.Text, "orderTotal", null)]
    [InlineData("", PlaceholderKind.Text, "", null)]
    [InlineData("customer.address.city", PlaceholderKind.Text, "customer.address.city", null)]
    // Qr
    [InlineData("qr:https://example.com", PlaceholderKind.Qr, "https://example.com", null)]
    [InlineData("qrcode:invoiceUrl", PlaceholderKind.Qr, "invoiceUrl", null)]
    [InlineData("QR:data", PlaceholderKind.Qr, "data", null)]
    [InlineData("Qr:data", PlaceholderKind.Qr, "data", null)]
    [InlineData("QRCODE:data", PlaceholderKind.Qr, "data", null)]
    [InlineData("QrCode:data", PlaceholderKind.Qr, "data", null)]
    [InlineData("  qr : trackingCode  ", PlaceholderKind.Qr, "trackingCode", null)]
    // Barcode
    [InlineData("barcode:productSku", PlaceholderKind.Barcode, "productSku", null)]
    [InlineData("BARCODE:sku123", PlaceholderKind.Barcode, "sku123", null)]
    // Image
    [InlineData("image:logoUrl", PlaceholderKind.Image, "logoUrl", null)]
    [InlineData("IMAGE:avatarField", PlaceholderKind.Image, "avatarField", null)]
    // Transform
    [InlineData("date:orderDate", PlaceholderKind.Transform, "date", "orderDate")]
    [InlineData("number:amount", PlaceholderKind.Transform, "number", "amount")]
    [InlineData("  upper : firstName  ", PlaceholderKind.Transform, "upper", "firstName")]
    public void Parse_WhenGivenInput_ShouldReturnExpectedPlaceholder(
        string raw,
        PlaceholderKind expectedKind,
        string expectedKey,
        string? expectedExtra)
    {
        var result = Parse(raw);
        result.Kind.Should().Be(expectedKind);
        result.Key.Should().Be(expectedKey);
        result.Extra.Should().Be(expectedExtra);
    }

    [Fact]
    public void Pattern_ShouldMatchDoubleCurlyBraces()
    {
        var input = "Dear {{customerName}}, your order {{orderId}} is ready.";
        var matches = Pattern.Matches(input);
        matches.Should().HaveCount(2);
        matches[0].Groups[1].Value.Should().Be("customerName");
        matches[1].Groups[1].Value.Should().Be("orderId");
    }

    [Fact]
    public void Pattern_ShouldNotMatchSingleCurlyBraces()
    {
        var input = "Hello {name} from {city}";
        var matches = Pattern.Matches(input);
        matches.Should().BeEmpty();
    }

    [Fact]
    public void Pattern_ShouldMatchQrCodePlaceholder()
    {
        var input = "Scan: {{qr:https://example.com/track?id=123}}";
        var match = Pattern.Match(input);
        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("qr:https://example.com/track?id=123");
    }

    [Fact]
    public void Pattern_ShouldNotMatchNestedBraces()
    {
        var input = "{{outer {{inner}} text}}";
        var matches = Pattern.Matches(input);
        matches.Should().HaveCount(1);
        matches[0].Groups[1].Value.Should().Be("inner");
    }
}

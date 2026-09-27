using FluentAssertions;
using SmkDoc.Application.Common.Helpers;
using Xunit;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Tests;

public class PlaceholderHelperTests
{
    // ── Parse — PlaceholderKind.Text ──────────────────────────────────────

    [Fact]
    public void Parse_SimpleKey_ReturnsTextKind()
    {
        var result = Parse("customerName");
        result.Kind.Should().Be(PlaceholderKind.Text);
        result.Key.Should().Be("customerName");
        result.Extra.Should().BeNull();
    }

    [Fact]
    public void Parse_KeyWithLeadingAndTrailingWhitespace_TrimsKey()
    {
        var result = Parse("  orderTotal  ");
        result.Kind.Should().Be(PlaceholderKind.Text);
        result.Key.Should().Be("orderTotal");
    }

    [Fact]
    public void Parse_EmptyString_ReturnsTextKindWithEmptyKey()
    {
        var result = Parse("");
        result.Kind.Should().Be(PlaceholderKind.Text);
        result.Key.Should().Be("");
    }

    [Fact]
    public void Parse_DotNotationKey_ReturnsTextKind()
    {
        var result = Parse("customer.address.city");
        result.Kind.Should().Be(PlaceholderKind.Text);
        result.Key.Should().Be("customer.address.city");
    }

    // ── Parse — PlaceholderKind.Qr ───────────────────────────────────────

    [Fact]
    public void Parse_QrPrefix_ReturnsQrKindWithRightSideAsKey()
    {
        var result = Parse("qr:https://example.com");
        result.Kind.Should().Be(PlaceholderKind.Qr);
        result.Key.Should().Be("https://example.com");
        result.Extra.Should().BeNull();
    }

    [Fact]
    public void Parse_QrcodePrefix_ReturnsQrKind()
    {
        var result = Parse("qrcode:invoiceUrl");
        result.Kind.Should().Be(PlaceholderKind.Qr);
        result.Key.Should().Be("invoiceUrl");
    }

    [Theory]
    [InlineData("QR:data")]
    [InlineData("Qr:data")]
    [InlineData("QRCODE:data")]
    [InlineData("QrCode:data")]
    public void Parse_QrPrefix_IsCaseInsensitive(string content)
    {
        var result = Parse(content);
        result.Kind.Should().Be(PlaceholderKind.Qr);
        result.Key.Should().Be("data");
    }

    [Fact]
    public void Parse_QrPrefixWithWhitespace_TrimsLeftRight()
    {
        var result = Parse("  qr : trackingCode  ");
        result.Kind.Should().Be(PlaceholderKind.Qr);
        result.Key.Should().Be("trackingCode");
    }

    // ── Parse — PlaceholderKind.Barcode ──────────────────────────────────

    [Fact]
    public void Parse_BarcodePrefix_ReturnsBarcodeKind()
    {
        var result = Parse("barcode:productSku");
        result.Kind.Should().Be(PlaceholderKind.Barcode);
        result.Key.Should().Be("productSku");
        result.Extra.Should().BeNull();
    }

    [Fact]
    public void Parse_BarcodePrefixUpperCase_ReturnsBarcodeKind()
    {
        var result = Parse("BARCODE:sku123");
        result.Kind.Should().Be(PlaceholderKind.Barcode);
        result.Key.Should().Be("sku123");
    }

    // ── Parse — PlaceholderKind.Image ─────────────────────────────────────

    [Fact]
    public void Parse_ImagePrefix_ReturnsImageKind()
    {
        var result = Parse("image:logoUrl");
        result.Kind.Should().Be(PlaceholderKind.Image);
        result.Key.Should().Be("logoUrl");
        result.Extra.Should().BeNull();
    }

    [Fact]
    public void Parse_ImagePrefixUpperCase_ReturnsImageKind()
    {
        var result = Parse("IMAGE:avatarField");
        result.Kind.Should().Be(PlaceholderKind.Image);
        result.Key.Should().Be("avatarField");
    }

    // ── Parse — PlaceholderKind.Transform ─────────────────────────────────

    [Fact]
    public void Parse_UnknownPrefixWithColon_ReturnsTransformKind()
    {
        var result = Parse("date:orderDate");
        result.Kind.Should().Be(PlaceholderKind.Transform);
        result.Key.Should().Be("date");
        result.Extra.Should().Be("orderDate");
    }

    [Fact]
    public void Parse_NumberTransform_ReturnsTransformKindWithExtras()
    {
        var result = Parse("number:amount");
        result.Kind.Should().Be(PlaceholderKind.Transform);
        result.Key.Should().Be("number");
        result.Extra.Should().Be("amount");
    }

    [Fact]
    public void Parse_TransformWithWhitespace_TrimsLeftAndRight()
    {
        var result = Parse("  upper : firstName  ");
        result.Kind.Should().Be(PlaceholderKind.Transform);
        result.Key.Should().Be("upper");
        result.Extra.Should().Be("firstName");
    }

    // ── Pattern (regex) ───────────────────────────────────────────────────

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
        // Pattern uses [^{}]+ so nested braces do not match
        matches.Should().HaveCount(1);
        matches[0].Groups[1].Value.Should().Be("inner");
    }
}

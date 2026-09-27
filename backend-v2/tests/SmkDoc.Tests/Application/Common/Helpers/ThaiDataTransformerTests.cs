using FluentAssertions;
using SmkDoc.Application.Common.Helpers;
using Xunit;

namespace SmkDoc.Tests.Application.Common.Helpers;

public class ThaiDataTransformerTests
{
    [Theory]
    [InlineData(0, "ศูนย์บาทถ้วน")]
    [InlineData(21, "ยี่สิบเอ็ดบาทถ้วน")]
    [InlineData(101, "หนึ่งร้อยเอ็ดบาทถ้วน")]
    [InlineData(2500000, "สองล้านห้าแสนบาทถ้วน")]
    [InlineData(10.50, "สิบบาทห้าสิบสตางค์")]
    [InlineData(-150.75, "ลบหนึ่งร้อยห้าสิบบาทเจ็ดสิบห้าสตางค์")]
    public void ToThaiBahtText_ShouldConvertAccurately(decimal amount, string expected)
    {
        string result = ThaiDataTransformer.ToThaiBahtText(amount);
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatThaiDate_ShouldConvertGregorianToBuddhistEra()
    {
        string result = ThaiDataTransformer.FormatThaiDate("2026-09-15");
        result.Should().Be("15 กันยายน 2569");
    }

    [Fact]
    public void FormatThaiDate_ShouldReturnOriginalString_WhenInvalidDate()
    {
        string result = ThaiDataTransformer.FormatThaiDate("invalid-date");
        result.Should().Be("invalid-date");
    }

    [Fact]
    public void FormatPhone_ShouldFormatStandardMobilePhone()
    {
        string result = ThaiDataTransformer.FormatPhone("0812345678");
        result.Should().Be("081-234-5678");
    }

    [Fact]
    public void FormatPhone_ShouldFormat9DigitPhone()
    {
        string result = ThaiDataTransformer.FormatPhone("021234567");
        result.Should().Be("02-123-4567");
    }

    [Fact]
    public void FormatPhone_ShouldReturnOriginal_WhenNot9Or10Digits()
    {
        string result = ThaiDataTransformer.FormatPhone("01234567");
        result.Should().Be("01234567");
    }

    [Fact]
    public void FormatThaiIdCard_ShouldFormat13Digits()
    {
        string result = ThaiDataTransformer.FormatThaiIdCard("1234567890123");
        result.Should().Be("1-2345-67890-12-3");
    }

    [Fact]
    public void FormatThaiIdCard_ShouldReturnOriginal_WhenNot13Digits()
    {
        string result = ThaiDataTransformer.FormatThaiIdCard("123456");
        result.Should().Be("123456");
    }
}

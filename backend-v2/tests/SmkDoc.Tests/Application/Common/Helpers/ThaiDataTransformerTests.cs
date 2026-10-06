using SmkDoc.Application.Common.Helpers;

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
    public void ToThaiBahtText_WhenGivenAmount_ConvertsAccurately(decimal amount, string expected)
    {
        var result = ThaiDataTransformer.ToThaiBahtText(amount);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-09-15", "15 กันยายน 2569")]
    [InlineData("invalid-date", "invalid-date")]
    [InlineData("", "")]
    public void FormatThaiDate_WhenGivenDateString_FormatsOrReturnsOriginal(string input, string expected)
    {
        var result = ThaiDataTransformer.FormatThaiDate(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0812345678", "081-234-5678")]
    [InlineData("021234567", "02-123-4567")]
    [InlineData("01234567", "01234567")]
    [InlineData("", "")]
    public void FormatPhone_WhenGivenVariousPhoneFormats_FormatsOrPreserves(string input, string expected)
    {
        var result = ThaiDataTransformer.FormatPhone(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1234567890123", "1-2345-67890-12-3")]
    [InlineData("123456", "123456")]
    [InlineData("", "")]
    public void FormatThaiIdCard_WhenGivenIdNumber_FormatsOrPreserves(string input, string expected)
    {
        var result = ThaiDataTransformer.FormatThaiIdCard(input);

        result.Should().Be(expected);
    }
}

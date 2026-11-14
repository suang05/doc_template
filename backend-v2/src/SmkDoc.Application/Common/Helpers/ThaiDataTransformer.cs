using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmkDoc.Application.Common.Helpers;

/// <summary>
/// Service providing high-performance Thai data transformation utilities for document generation:
/// - Thai Baht text conversion (e.g. 2,500,000 -> สองล้านห้าแสนบาทถ้วน)
/// - Thai Buddhist Era date formatting (e.g. 2026-09-09 -> 9 กันยายน 2569)
/// - Currency, phone number, and Thai national ID card formatting
/// </summary>
public static partial class ThaiDataTransformer
{
    private static readonly string[] Units = ["", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];
    private static readonly string[] Digits = ["", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน", "ล้าน"];

    private static readonly string[] FullThaiMonths =
    [
        "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน",
        "พฤษภาคม", "มิถุนายน", "กรกฎาคม", "สิงหาคม",
        "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    ];

    private static readonly string[] AbbrThaiMonths =
    [
        "", "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.",
        "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.",
        "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."
    ];

    [GeneratedRegex(@"^(?<y>\d{4})[-/](?<m>\d{1,2})[-/](?<d>\d{1,2})(?:[T\s](?<rest>.+))?$")]
    private static partial Regex YearFirstDateRegex();

    [GeneratedRegex(@"^(?<d>\d{1,2})[-/](?<m>\d{1,2})[-/](?<y>\d{4})(?:[T\s](?<rest>.+))?$")]
    private static partial Regex DayFirstDateRegex();

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Explicit lowercase template transform requested by caller.")]
    public static string Transform(string? value, string transformType)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string normalizedType = transformType.Trim().ToUpperInvariant();

        return normalizedType switch
        {
            "BAHT" or "THAIBAHT" or "CURRENCY_THAI" or "THAI_BAHT" or "THAI_BAHT_TEXT" or "BAHT_TEXT" or "BAHTTEXT" => ToThaiBahtText(value),
            "CURRENCY" or "MONEY" or "NUMBER" => FormatCurrency(value, 2),
            "CURRENCY0" => FormatCurrency(value, 0),
            "DATE" or "THAIDATE" or "THAI_DATE" or "THAIDATE_FULL" => FormatThaiDate(value, abbreviated: false),
            "THAIDATE_SHORT" or "THAI_DATE_ABBR" or "THAIDATESHORT" => FormatThaiDate(value, abbreviated: true),
            "THAIDATETIME" or "THAI_DATETIME" => FormatThaiDateTime(value, abbreviated: false),
            "UPPER" => value.ToUpperInvariant(),
            "LOWER" => value.ToLowerInvariant(),
            "PHONE" or "THAIPHONE" or "TEL" => FormatPhone(value),
            "IDCARD" or "THAIID" or "CITIZEN_ID" => FormatThaiIdCard(value),
            _ => value
        };
    }

    public static string ToThaiBahtText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string cleaned = input.Replace(",", "", StringComparison.Ordinal).Replace("฿", "", StringComparison.Ordinal).Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
        {
            return input;
        }

        return ToThaiBahtText(amount);
    }

    public static string ToThaiBahtText(decimal amount)
    {
        if (amount == 0) return "ศูนย์บาทถ้วน";

        bool isNegative = amount < 0;
        if (isNegative) amount = Math.Abs(amount);

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        long baht = (long)Math.Floor(amount);
        int satang = (int)Math.Round((amount - baht) * 100);

        var result = new StringBuilder();
        if (isNegative) result.Append("ลบ");

        if (baht > 0)
        {
            result.Append(ConvertIntegerToThaiText(baht));
            result.Append("บาท");
        }

        if (satang > 0)
        {
            result.Append(ConvertIntegerToThaiText(satang));
            result.Append("สตางค์");
        }
        else
        {
            result.Append("ถ้วน");
        }

        return result.ToString();
    }

    private static string ConvertIntegerToThaiText(long number)
    {
        if (number == 0) return "ศูนย์";

        var result = new StringBuilder();
        string numStr = number.ToString(CultureInfo.InvariantCulture);
        int len = numStr.Length;

        if (len > 6)
        {
            long millions = number / 1_000_000;
            long remainder = number % 1_000_000;

            result.Append(ConvertIntegerToThaiText(millions));
            result.Append("ล้าน");

            if (remainder > 0)
            {
                result.Append(ConvertGroupToThaiText(remainder.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0'), hasHigherDigits: true));
            }
            return result.ToString();
        }

        return ConvertGroupToThaiText(numStr, hasHigherDigits: false);
    }

    private static string ConvertGroupToThaiText(string numStr, bool hasHigherDigits)
    {
        var result = new StringBuilder();
        int len = numStr.Length;
        bool hadPrecedingNonZero = hasHigherDigits;

        for (int i = 0; i < len; i++)
        {
            int digit = numStr[i] - '0';
            if (digit == 0) continue;

            int position = len - i - 1;
            hadPrecedingNonZero = AppendThaiDigit(result, digit, position, hadPrecedingNonZero);
        }

        return result.ToString();
    }

    private static bool AppendThaiDigit(StringBuilder result, int digit, int position, bool hadPrecedingNonZero)
    {
        switch (position)
        {
            case 1:
                AppendTensDigit(result, digit);
                return true;
            case 0:
                AppendOnesDigit(result, digit, hadPrecedingNonZero);
                return hadPrecedingNonZero;
            default:
                result.Append(Units[digit]).Append(Digits[position]);
                return true;
        }
    }

    private static void AppendTensDigit(StringBuilder result, int digit)
    {
        if (digit == 1)
        {
            result.Append("สิบ");
            return;
        }

        if (digit == 2)
        {
            result.Append("ยี่สิบ");
            return;
        }

        result.Append(Units[digit]).Append("สิบ");
    }

    private static void AppendOnesDigit(StringBuilder result, int digit, bool hadPrecedingNonZero)
    {
        result.Append(digit == 1 && hadPrecedingNonZero ? "เอ็ด" : Units[digit]);
    }

    public static string FormatThaiDate(string input, bool abbreviated = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        if (!TryParseGregorianDate(input.Trim(), out DateTime date))
        {
            return input;
        }

        int thaiYear = date.Year > 2400 ? date.Year : date.Year + 543;
        string month = abbreviated ? AbbrThaiMonths[date.Month] : FullThaiMonths[date.Month];
        return $"{date.Day} {month} {thaiYear}";
    }

    public static string FormatThaiDateTime(string input, bool abbreviated = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        if (!TryParseGregorianDate(input.Trim(), out DateTime date))
        {
            return input;
        }

        int thaiYear = date.Year > 2400 ? date.Year : date.Year + 543;
        string month = abbreviated ? AbbrThaiMonths[date.Month] : FullThaiMonths[date.Month];
        return $"{date.Day} {month} {thaiYear} {date:HH:mm} น.";
    }

    private static bool TryParseGregorianDate(string trimmed, out DateTime gregorianDate)
    {
        // 1. Pre-normalize Buddhist Era years (> 2400 => - 543) BEFORE Gregorian calendar validation
        //    so Buddhist Era leap-year dates (e.g., 2567-02-29 or 29/02/2567) resolve to 2024-02-29.
        if (TryParseMatchedDate(YearFirstDateRegex().Match(trimmed), requireYearOrDayThreshold: false, out gregorianDate))
        {
            return true;
        }

        // When Buddhist Era year (> 2400) or unambiguous day > 12 is used, interpret strictly as dd/MM/yyyy.
        if (TryParseMatchedDate(DayFirstDateRegex().Match(trimmed), requireYearOrDayThreshold: true, out gregorianDate))
        {
            return true;
        }

        // 2. Fallback to standard Gregorian parsing for ISO/Invariant formats
        return TryParseFallbackDate(trimmed, out gregorianDate);
    }

    private static bool TryParseMatchedDate(
        Match match,
        bool requireYearOrDayThreshold,
        out DateTime gregorianDate)
    {
        gregorianDate = default;
        if (!match.Success ||
            !int.TryParse(match.Groups["y"].Value, out int rawYear) ||
            !int.TryParse(match.Groups["m"].Value, out int month) ||
            !int.TryParse(match.Groups["d"].Value, out int day))
        {
            return false;
        }

        if (requireYearOrDayThreshold && rawYear <= 2400 && day <= 12)
        {
            return false;
        }

        int normalizedYear = rawYear > 2400 ? rawYear - 543 : rawYear;
        return TryBuildDateWithOptionalTime(
            normalizedYear,
            month,
            day,
            match.Groups["rest"].Value,
            out gregorianDate);
    }

    private static bool TryParseFallbackDate(string trimmed, out DateTime gregorianDate)
    {
        if (!DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out gregorianDate) &&
            !DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out gregorianDate))
        {
            return false;
        }

        if (gregorianDate.Year <= 2400)
        {
            return true;
        }

        string? timeRest = gregorianDate.TimeOfDay == TimeSpan.Zero
            ? string.Empty
            : gregorianDate.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        return TryBuildDateWithOptionalTime(
            gregorianDate.Year - 543,
            gregorianDate.Month,
            gregorianDate.Day,
            timeRest,
            out gregorianDate);
    }

    private static bool TryBuildDateWithOptionalTime(
        int gregorianYear,
        int month,
        int day,
        string? timeRest,
        out DateTime result)
    {
        result = default;
        if (gregorianYear is < 1 or > 9999 || month is < 1 or > 12)
        {
            return false;
        }

        int maxDay = DateTime.DaysInMonth(gregorianYear, month);
        if (day < 1 || day > maxDay)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(timeRest))
        {
            result = new DateTime(gregorianYear, month, day, 0, 0, 0, DateTimeKind.Unspecified);
            return true;
        }

        string isoCandidate = $"{gregorianYear:D4}-{month:D2}-{day:D2}T{timeRest.Trim()}";
        if (DateTimeOffset.TryParse(isoCandidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            result = dto.DateTime;
            return true;
        }

        if (DateTime.TryParse(isoCandidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        return false;
    }

    public static string FormatCurrency(string input, int decimalPlaces = 2)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string cleaned = input.Replace(",", "", StringComparison.Ordinal).Replace("฿", "", StringComparison.Ordinal).Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
        {
            return input;
        }

        return decimalPlaces > 0
            ? amount.ToString($"N{decimalPlaces}", CultureInfo.InvariantCulture)
            : amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"\D", RegexOptions.None, 500)]
    private static partial Regex NonDigitRegex();

    public static string FormatPhone(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string digits = NonDigitRegex().Replace(input, "");
        if (digits.Length == 10)
        {
            return $"{digits[..3]}-{digits[3..6]}-{digits[6..]}";
        }
        if (digits.Length == 9)
        {
            return $"{digits[..2]}-{digits[2..5]}-{digits[5..]}";
        }
        return input;
    }

    public static string FormatThaiIdCard(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string digits = NonDigitRegex().Replace(input, "");
        if (digits.Length == 13)
        {
            return $"{digits[0]}-{digits[1..5]}-{digits[5..10]}-{digits[10..12]}-{digits[12]}";
        }
        return input;
    }
}

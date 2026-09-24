using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Service providing high-performance Thai data transformation utilities for document generation:
/// - Thai Baht text conversion (e.g. 2,500,000 -> สองล้านห้าแสนบาทถ้วน)
/// - Thai Buddhist Era date formatting (e.g. 2026-09-09 -> 9 กันยายน 2569)
/// - Currency, phone number, and Thai national ID card formatting
/// </summary>
public static class ThaiDataTransformer
{
    private static readonly string[] Units = { "", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า" };
    private static readonly string[] Digits = { "", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน", "ล้าน" };

    private static readonly string[] FullThaiMonths = {
        "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน",
        "พฤษภาคม", "มิถุนายน", "กรกฎาคม", "สิงหาคม",
        "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    };

    private static readonly string[] AbbrThaiMonths = {
        "", "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.",
        "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.",
        "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."
    };

    /// <summary>
    /// Transforms an input string or object based on a transform directive.
    /// Supported transforms:
    /// - "baht", "thaibaht", "currency_thai": 2500000 -> "สองล้านห้าแสนบาทถ้วน"
    /// - "currency", "money": 2500000 -> "2,500,000.00"
    /// - "currency0": 2500000 -> "2,500,000"
    /// - "thaidate", "thai_date": 2026-09-09 -> "9 กันยายน 2569"
    /// - "thaidate_short", "thai_date_abbr": 2026-09-09 -> "9 ก.ย. 2569"
    /// - "thaidatetime", "thai_datetime": 2026-09-09T14:30:00 -> "9 กันยายน 2569 14:30 น."
    /// - "phone", "thaiphone": 0812345678 -> "081-234-5678"
    /// - "idcard", "thaiid": 1234567890123 -> "1-2345-67890-12-3"
    /// </summary>
    public static string Transform(string? value, string transformType)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string normalizedType = transformType.Trim().ToLowerInvariant();

        return normalizedType switch
        {
            "baht" or "thaibaht" or "currency_thai" or "thai_baht" => ToThaiBahtText(value),
            "currency" or "money" => FormatCurrency(value, 2),
            "currency0" => FormatCurrency(value, 0),
            "thaidate" or "thai_date" or "thaidate_full" => FormatThaiDate(value, abbreviated: false),
            "thaidate_short" or "thai_date_abbr" or "thaidateshort" => FormatThaiDate(value, abbreviated: true),
            "thaidatetime" or "thai_datetime" => FormatThaiDateTime(value, abbreviated: false),
            "phone" or "thaiphone" or "tel" => FormatPhone(value),
            "idcard" or "thaiid" or "citizen_id" => FormatThaiIdCard(value),
            _ => value
        };
    }

    /// <summary>
    /// Converts a number into Thai Baht text format (e.g. 2,500,000.00 -> สองล้านห้าแสนบาทถ้วน)
    /// </summary>
    public static string ToThaiBahtText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Clean input from currency symbols and commas
        string cleaned = input.Replace(",", "").Replace("฿", "").Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
        {
            return input;
        }

        return ToThaiBahtText(amount);
    }

    /// <summary>
    /// Converts decimal into Thai Baht text format
    /// </summary>
    public static string ToThaiBahtText(decimal amount)
    {
        if (amount == 0) return "ศูนย์บาทถ้วน";

        bool isNegative = amount < 0;
        if (isNegative) amount = Math.Abs(amount);

        // Round to 2 decimal places
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
        string numStr = number.ToString();
        int len = numStr.Length;

        // If number > 1,000,000, recurse for millions
        if (len > 6)
        {
            long millions = number / 1_000_000;
            long remainder = number % 1_000_000;

            result.Append(ConvertIntegerToThaiText(millions));
            result.Append("ล้าน");

            if (remainder > 0)
            {
                result.Append(ConvertGroupToThaiText(remainder.ToString().PadLeft(6, '0'), hasHigherDigits: true));
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
            int position = len - i - 1; // 0=หน่วย, 1=สิบ, 2=ร้อย, ...

            if (digit == 0) continue;

            if (position == 1) // หลักสิบ
            {
                hadPrecedingNonZero = true;
                if (digit == 1)
                {
                    result.Append("สิบ");
                }
                else if (digit == 2)
                {
                    result.Append("ยี่สิบ");
                }
                else
                {
                    result.Append(Units[digit]).Append("สิบ");
                }
            }
            else if (position == 0) // หลักหน่วย
            {
                if (digit == 1 && hadPrecedingNonZero)
                {
                    result.Append("เอ็ด");
                }
                else
                {
                    result.Append(Units[digit]);
                }
            }
            else
            {
                hadPrecedingNonZero = true;
                result.Append(Units[digit]).Append(Digits[position]);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Formats a date string into Thai Buddhist Era format (e.g. 2026-09-09 -> 9 กันยายน 2569)
    /// </summary>
    public static string FormatThaiDate(string input, bool abbreviated = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        if (!DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) &&
            !DateTime.TryParse(input, out date))
        {
            return input;
        }

        int thaiYear = date.Year > 2400 ? date.Year : date.Year + 543;
        string month = abbreviated ? AbbrThaiMonths[date.Month] : FullThaiMonths[date.Month];
        return $"{date.Day} {month} {thaiYear}";
    }

    /// <summary>
    /// Formats a datetime string into Thai format (e.g. 2026-09-09T14:30:00 -> 9 กันยายน 2569 14:30 น.)
    /// </summary>
    public static string FormatThaiDateTime(string input, bool abbreviated = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        if (!DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) &&
            !DateTime.TryParse(input, out date))
        {
            return input;
        }

        string datePart = FormatThaiDate(input, abbreviated);
        return $"{datePart} {date:HH:mm} น.";
    }

    /// <summary>
    /// Formats number with commas and decimal places (e.g. 2500000 -> 2,500,000.00)
    /// </summary>
    public static string FormatCurrency(string input, int decimalPlaces = 2)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string cleaned = input.Replace(",", "").Replace("฿", "").Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
        {
            return input;
        }

        return decimalPlaces > 0
            ? amount.ToString($"N{decimalPlaces}", CultureInfo.InvariantCulture)
            : amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats telephone number (e.g. 0812345678 -> 081-234-5678, 021234567 -> 02-123-4567)
    /// </summary>
    public static string FormatPhone(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string digits = Regex.Replace(input, @"\D", "");
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

    /// <summary>
    /// Formats Thai Citizen National ID Card (13 digits: 1-2345-67890-12-3)
    /// </summary>
    public static string FormatThaiIdCard(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string digits = Regex.Replace(input, @"\D", "");
        if (digits.Length == 13)
        {
            return $"{digits[0]}-{digits[1..5]}-{digits[5..10]}-{digits[10..12]}-{digits[12]}";
        }
        return input;
    }
}

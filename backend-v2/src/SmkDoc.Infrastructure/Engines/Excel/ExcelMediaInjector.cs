using ClosedXML.Excel;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Imaging;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Infrastructure.Engines.Excel;

public class ExcelMediaInjector
{
    private readonly IMediaGenerationService _mediaService;
    private const int QrSizePx   = 150;
    private const int BarcodeW   = 300;
    private const int BarcodeH   = 100;
    private const double PxToRowH = 0.75;  // Excel row height (pt) ≈ 0.75 × px
    private const double PxToColW = 1.0 / 7.0; // Excel column width unit ≈ px / 7

    public ExcelMediaInjector(IMediaGenerationService mediaService)
    {
        _mediaService = mediaService;
    }

    /// <summary>
    /// Scans cells for {{qr:key}} and {{barcode:key}}, generates images, and embeds them
    /// anchored to the cell. The cell text is cleared after embedding.
    /// </summary>
    public void InjectMedia(IXLWorksheet worksheet, Dictionary<string, string> data)
    {
        var targets = CollectMediaCells(worksheet);

        foreach (var (row, col, kind, key) in targets)
        {
            if (!data.TryGetValue(key, out var text)) continue;

            byte[] imageBytes;
            int w, h;
            if (kind == PlaceholderKind.Qr)
            {
                w = h = QrSizePx;
                imageBytes = _mediaService.GenerateQrCode(text, w, h);
            }
            else
            {
                w = BarcodeW; h = BarcodeH;
                imageBytes = _mediaService.GenerateBarcode(text, w, h);
            }

            var cell = worksheet.Cell(row, col);
            cell.SetValue(string.Empty);

            // Expand row/column to fit the image
            worksheet.Row(row).Height    = Math.Max(worksheet.Row(row).Height,    h * PxToRowH);
            worksheet.Column(col).Width  = Math.Max(worksheet.Column(col).Width,  w * PxToColW);

            try
            {
                using var imgStream = new MemoryStream(imageBytes);
                worksheet.AddPicture(imgStream)
                    .MoveTo(cell)
                    .WithSize(w, h);
            }
            catch (ArgumentException)
            {
                // Invalid or empty image bytes — skip embedding; cell is already cleared
            }
        }
    }

    private static List<(int row, int col, PlaceholderKind kind, string key)> CollectMediaCells(
        IXLWorksheet worksheet)
    {
        var result = new List<(int, int, PlaceholderKind, string)>();
        foreach (var cell in worksheet.CellsUsed())
        {
            if (cell.HasFormula) continue;
            var val = cell.GetString();
            if (!val.Contains("{{")) continue;

            foreach (System.Text.RegularExpressions.Match m in Pattern.Matches(val))
            {
                var parsed = Parse(m.Groups[1].Value);
                if (parsed.Kind is PlaceholderKind.Qr or PlaceholderKind.Barcode)
                    result.Add((cell.Address.RowNumber, cell.Address.ColumnNumber, parsed.Kind, parsed.Key));
            }
        }
        return result;
    }
}

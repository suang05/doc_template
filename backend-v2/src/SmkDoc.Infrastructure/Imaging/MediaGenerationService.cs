using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Imaging;

public class MediaGenerationService(
    IQrCodeService qrCodeService,
    IBarcodeService barcodeService,
    IImageOptimizer? imageOptimizer = null) : IMediaGenerationService
{
    private readonly IImageOptimizer _imageOptimizer = imageOptimizer ?? new ImageOptimizerService();

    public byte[] GenerateQrCode(string text, int widthPx = 150, int heightPx = 150, bool optimize = true)
    {
        string safeText = string.IsNullOrWhiteSpace(text) ? "SAMPLE-QR" : text;
        byte[] raw = qrCodeService.Generate(safeText, 10);
        return optimize ? _imageOptimizer.Optimize(raw, widthPx, heightPx) : raw;
    }

    public byte[] GenerateBarcode(string text, int widthPx = 300, int heightPx = 100, bool optimize = true)
    {
        string safeText = string.IsNullOrWhiteSpace(text) ? "SAMPLE-128" : text;
        byte[] raw = barcodeService.Generate(safeText, widthPx, heightPx);
        return optimize ? _imageOptimizer.Optimize(raw, widthPx, heightPx) : raw;
    }

    public string GenerateQrCodeDataUri(string text, int widthPx = 150, int heightPx = 150)
    {
        byte[] bytes = GenerateQrCode(text, widthPx, heightPx, optimize: true);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    public string GenerateBarcodeDataUri(string text, int widthPx = 300, int heightPx = 100)
    {
        byte[] bytes = GenerateBarcode(text, widthPx, heightPx, optimize: true);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}

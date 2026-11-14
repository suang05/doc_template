using Microsoft.Extensions.Logging;
using SkiaSharp;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Imaging;

public sealed partial class ImageOptimizerService(ILogger<ImageOptimizerService>? logger = null) : IImageOptimizer
{
    private const int DefaultMaxPixelDimension = 1920;
    private const int JpegQuality = 92;
    private const int MinSizeForOptimization = 50 * 1024; // 50 KB
    private const double PrintDpiMultiplier = 2.0;        // 2× display → print quality

    public byte[] Optimize(byte[] imageBytes, int targetWidthPx, int targetHeightPx)
    {
        if (imageBytes == null || imageBytes.Length < MinSizeForOptimization)
            return imageBytes ?? [];

        try
        {
            return OptimizeCore(imageBytes, targetWidthPx, targetHeightPx);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            LogOptimizeFailure(ex, imageBytes.Length);
            return imageBytes;
        }
    }

    private byte[] OptimizeCore(byte[] imageBytes, int targetWidthPx, int targetHeightPx)
    {
        using var original = SKBitmap.Decode(imageBytes);
        if (original == null) return imageBytes;

        var (maxW, maxH) = ComputeMaxDimensions(targetWidthPx, targetHeightPx);
        bool needsResize = original.Width > maxW || original.Height > maxH;
        bool hasAlpha = HasTransparency(original);

        if (!needsResize && IsJpeg(imageBytes))
            return imageBytes;

        var (newW, newH) = needsResize
            ? ScaleProportionally(original.Width, original.Height, maxW, maxH)
            : (original.Width, original.Height);

        using var resized = needsResize
            ? original.Resize(
                new SKImageInfo(newW, newH, original.ColorType, original.AlphaType),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            : null;

        if (needsResize && resized == null) return imageBytes;

        var optimized = Encode(resized ?? original, hasAlpha);
        if (optimized.Length >= imageBytes.Length) return imageBytes;

        LogOptimizationSavings(original, newW, newH, imageBytes.Length, optimized.Length, hasAlpha);
        return optimized;
    }

    private void LogOptimizationSavings(
        SKBitmap original,
        int newW,
        int newH,
        int originalLength,
        int optimizedLength,
        bool hasAlpha)
    {
        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            var pctSaved = ((1.0 - (double)optimizedLength / originalLength) * 100.0)
                .ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            LogImageOptimized(
                logger,
                original.Width,
                original.Height,
                newW,
                newH,
                originalLength / 1024,
                optimizedLength / 1024,
                pctSaved,
                hasAlpha);
        }
    }

    private void LogOptimizeFailure(Exception ex, int byteLength)
    {
        if (logger is not null && logger.IsEnabled(LogLevel.Warning))
        {
            WriteOptimizeFailure(logger, ex, byteLength / 1024);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ImageOptimizer] {OrigW}x{OrigH} → {NewW}x{NewH} | {OrigKB}KB → {NewKB}KB ({Pct}% saved, alpha={Alpha})")]
    private static partial void LogImageOptimized(
        ILogger logger,
        int origW,
        int origH,
        int newW,
        int newH,
        int origKB,
        int newKB,
        string pct,
        bool alpha);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ImageOptimizer] Failed to optimize ({KB}KB) — using original")]
    private static partial void WriteOptimizeFailure(ILogger logger, Exception ex, int kb);

    private static (int maxW, int maxH) ComputeMaxDimensions(int wPx, int hPx)
    {
        int maxW = wPx > 0 ? Math.Max(100, (int)(wPx * PrintDpiMultiplier)) : DefaultMaxPixelDimension;
        int maxH = hPx > 0 ? Math.Max(100, (int)(hPx * PrintDpiMultiplier)) : DefaultMaxPixelDimension;
        return (maxW, maxH);
    }

    private static (int w, int h) ScaleProportionally(int origW, int origH, int maxW, int maxH)
    {
        double scale = Math.Min((double)maxW / origW, (double)maxH / origH);
        return (Math.Max(1, (int)(origW * scale)), Math.Max(1, (int)(origH * scale)));
    }

    private static byte[] Encode(SKBitmap bitmap, bool hasAlpha)
    {
        using var image = SKImage.FromBitmap(bitmap);
        var format = hasAlpha ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        var quality = hasAlpha ? 100 : JpegQuality;
        using var encoded = image.Encode(format, quality);
        return encoded.ToArray();
    }

    private static bool HasTransparency(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque) return false;
        int step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 50);
        for (int y = 0; y < bitmap.Height; y += step)
            for (int x = 0; x < bitmap.Width; x += step)
                if (bitmap.GetPixel(x, y).Alpha < 255)
                    return true;
        return false;
    }

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
}

using Microsoft.Extensions.Logging;
using SkiaSharp;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Imaging;

public sealed class ImageOptimizerService(ILogger<ImageOptimizerService>? logger = null) : IImageOptimizer
{
    private const int DefaultMaxPixelDimension = 1920;
    private const int JpegQuality = 92;
    private const int MinSizeForOptimization = 50 * 1024; // 50 KB
    private const double PrintDpiMultiplier = 2.0;        // 2× display → print quality

    public byte[] Optimize(byte[] imageBytes, int targetWidthPx = 0, int targetHeightPx = 0)
    {
        if (imageBytes == null || imageBytes.Length < MinSizeForOptimization)
            return imageBytes ?? [];

        try
        {
            using var original = SKBitmap.Decode(imageBytes);
            if (original == null) return imageBytes;

            var (maxW, maxH) = ComputeMaxDimensions(targetWidthPx, targetHeightPx);

            bool needsResize = original.Width > maxW || original.Height > maxH;
            bool hasAlpha    = HasTransparency(original);

            if (!needsResize && IsJpeg(imageBytes))
                return imageBytes;

            var (newW, newH) = needsResize
                ? ScaleProportionally(original.Width, original.Height, maxW, maxH)
                : (original.Width, original.Height);

            SKBitmap? resized = needsResize
                ? original.Resize(new SKImageInfo(newW, newH, original.ColorType, original.AlphaType),
                                   new SKSamplingOptions(SKCubicResampler.Mitchell))
                : null;

            if (needsResize && resized == null) return imageBytes;

            SKBitmap target = resized ?? original;

            try
            {
                var optimized = Encode(target, hasAlpha);
                if (optimized.Length >= imageBytes.Length) return imageBytes;

                logger?.LogInformation(
                    "[ImageOptimizer] {OrigW}x{OrigH} → {NewW}x{NewH} | {OrigKB}KB → {NewKB}KB ({Pct:F1}% saved, alpha={Alpha})",
                    original.Width, original.Height, newW, newH,
                    imageBytes.Length / 1024, optimized.Length / 1024,
                    (1.0 - (double)optimized.Length / imageBytes.Length) * 100, hasAlpha);

                return optimized;
            }
            finally
            {
                resized?.Dispose();
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[ImageOptimizer] Failed to optimize ({KB}KB) — using original", imageBytes.Length / 1024);
            return imageBytes;
        }
    }

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

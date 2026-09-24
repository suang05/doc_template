using SkiaSharp;
using SmkDoc.Application.Common.Interfaces;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using ZXing.SkiaSharp.Rendering;

namespace SmkDoc.Infrastructure.Imaging;

public sealed class BarcodeService : IBarcodeService
{
    public byte[] Generate(string text, int widthPx = 300, int heightPx = 100)
    {
        string content = string.IsNullOrWhiteSpace(text) ? "SAMPLE" : text.Trim();
        // CODE_128 only supports ASCII printable characters (32-126)
        string sanitized = new string(content.Where(c => c >= 32 && c <= 126).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "SAMPLE";
        }

        try
        {
            var writer = new BarcodeWriter
            {
                Format  = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width       = widthPx > 0 ? widthPx : 300,
                    Height      = heightPx > 0 ? heightPx : 100,
                    Margin      = 10,
                    PureBarcode = false
                },
                Renderer = new SKBitmapRenderer()
            };

            using var bitmap  = writer.Write(sanitized);
            using var image   = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
        catch
        {
            var fallbackWriter = new BarcodeWriter
            {
                Format  = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width       = widthPx > 0 ? widthPx : 300,
                    Height      = heightPx > 0 ? heightPx : 100,
                    Margin      = 10,
                    PureBarcode = false
                },
                Renderer = new SKBitmapRenderer()
            };
            using var fbBitmap  = fallbackWriter.Write("SAMPLE");
            using var fbImage   = SKImage.FromBitmap(fbBitmap);
            using var fbEncoded = fbImage.Encode(SKEncodedImageFormat.Png, 100);
            return fbEncoded.ToArray();
        }
    }
}

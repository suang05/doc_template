using QRCoder;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Imaging;

public sealed class QrCodeService : IQrCodeService
{
    public byte[] Generate(string text, int pixelSize)
    {
        string content = string.IsNullOrWhiteSpace(text) ? "SAMPLE" : text;
        int effectivePixelSize = pixelSize > 0 ? pixelSize : 10;
        try
        {
            return RenderQrPng(content, effectivePixelSize);
        }
        catch (QRCoder.Exceptions.DataTooLongException)
        {
            return RenderQrPng("SAMPLE", 10);
        }
        catch (ArgumentException)
        {
            return RenderQrPng("SAMPLE", 10);
        }
        catch (InvalidOperationException)
        {
            return RenderQrPng("SAMPLE", 10);
        }
    }

    private static byte[] RenderQrPng(string content, int pixelSize)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelSize);
    }
}

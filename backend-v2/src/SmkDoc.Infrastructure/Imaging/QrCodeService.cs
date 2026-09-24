using QRCoder;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Imaging;

public sealed class QrCodeService : IQrCodeService
{
    public byte[] Generate(string text, int pixelSize = 10)
    {
        string content = string.IsNullOrWhiteSpace(text) ? "SAMPLE" : text;
        try
        {
            using var generator = new QRCodeGenerator();
            using var data      = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var qrCode    = new PngByteQRCode(data);
            return qrCode.GetGraphic(pixelSize > 0 ? pixelSize : 10);
        }
        catch
        {
            using var generator = new QRCodeGenerator();
            using var data      = generator.CreateQrCode("SAMPLE", QRCodeGenerator.ECCLevel.Q);
            using var qrCode    = new PngByteQRCode(data);
            return qrCode.GetGraphic(10);
        }
    }
}

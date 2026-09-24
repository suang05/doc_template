namespace SmkDoc.Application.Common.Interfaces;

public interface IQrCodeService
{
    byte[] Generate(string text, int pixelSize);
}

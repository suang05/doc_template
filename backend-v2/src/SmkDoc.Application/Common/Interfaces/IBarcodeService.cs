namespace SmkDoc.Application.Common.Interfaces;

public interface IBarcodeService
{
    byte[] Generate(string text, int widthPx, int heightPx);
}

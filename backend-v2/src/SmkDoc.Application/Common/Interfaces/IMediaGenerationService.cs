namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Unified service for generating and optimizing QR codes and Barcodes across HTML, Word, and Excel engines.
/// </summary>
public interface IMediaGenerationService
{
    /// <summary>
    /// Generates optimized PNG bytes for a QR Code.
    /// </summary>
    byte[] GenerateQrCode(string text, int widthPx = 150, int heightPx = 150, bool optimize = true);

    /// <summary>
    /// Generates optimized PNG bytes for a Code-128 Barcode.
    /// </summary>
    byte[] GenerateBarcode(string text, int widthPx = 300, int heightPx = 100, bool optimize = true);

    /// <summary>
    /// Generates an HTML data URI image string (data:image/png;base64,...) for a QR Code.
    /// </summary>
    string GenerateQrCodeDataUri(string text, int widthPx = 150, int heightPx = 150);

    /// <summary>
    /// Generates an HTML data URI image string (data:image/png;base64,...) for a Code-128 Barcode.
    /// </summary>
    string GenerateBarcodeDataUri(string text, int widthPx = 300, int heightPx = 100);
}

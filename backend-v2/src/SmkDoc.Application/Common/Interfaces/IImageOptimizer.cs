namespace SmkDoc.Application.Common.Interfaces;

public interface IImageOptimizer
{
    /// <summary>
    /// Optimizes image bytes for embedding in a document.
    /// Resizes to print quality (2× display DPI) and re-encodes to reduce file size.
    /// Returns the original bytes if optimization yields no benefit.
    /// </summary>
    byte[] Optimize(byte[] imageBytes, int targetWidthPx, int targetHeightPx);
}

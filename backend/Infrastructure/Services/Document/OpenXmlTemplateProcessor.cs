using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Domain.Models;

namespace SmkDocServer.Infrastructure.Services.Document;

public class OpenXmlTemplateProcessor : ITemplateProcessor
{
    private readonly IDocxProcessingService _docxService;
    private readonly IExcelProcessingService _excelService;
    private readonly IPdfConverter _pdfConverter;

    public OpenXmlTemplateProcessor(
        IDocxProcessingService docxService,
        IExcelProcessingService excelService,
        IPdfConverter pdfConverter)
    {
        _docxService  = docxService;
        _excelService = excelService;
        _pdfConverter = pdfConverter;
    }

    public TemplateEngineType EngineType => TemplateEngineType.OpenXML;

    public async Task<byte[]> ProcessAsync(TemplateProcessingContext ctx, CancellationToken cancellationToken = default)
    {
        var format = ctx.Template.Format.ToLowerInvariant();
        byte[] nativeBytes;

        using (var templateStream = File.OpenRead(ctx.TemplatePath))
        {
            Stream processed;
            if (format == ".docx")
            {
                // PreprocessForPreviewAsync improves Thai font rendering in preview mode
                var input = ctx.IsPreview
                    ? await _docxService.PreprocessForPreviewAsync(templateStream)
                    : templateStream;
                processed = await _docxService.ProcessTemplateAsync(input, ctx.Data);
            }
            else
            {
                processed = await _excelService.ProcessTemplateAsync(templateStream, ctx.Data);
            }

            nativeBytes = await DrainAsync(processed, cancellationToken);
            await processed.DisposeAsync();
        }

        if (string.Equals(ctx.OutputFormat, "pdf", StringComparison.OrdinalIgnoreCase))
            return await _pdfConverter.ConvertToPdfAsync(nativeBytes, ctx.Template.FileName);

        return nativeBytes;
    }

    private static async Task<byte[]> DrainAsync(Stream s, CancellationToken ct)
    {
        if (s is MemoryStream ms) return ms.ToArray();
        using var buf = new MemoryStream();
        await s.CopyToAsync(buf, ct);
        return buf.ToArray();
    }
}

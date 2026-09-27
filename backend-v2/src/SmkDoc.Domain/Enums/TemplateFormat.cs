using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class TemplateFormat : Enumeration
{
    public static readonly TemplateFormat Html = new(1, "html", "text/html", RenderEngineType.Html);
    public static readonly TemplateFormat Docx = new(2, "docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", RenderEngineType.Docx);
    public static readonly TemplateFormat Xlsx = new(3, "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", RenderEngineType.Excel);
    public static readonly TemplateFormat Pdf  = new(4, "pdf", "application/pdf", RenderEngineType.Html);

    public string Extension { get; }
    public string MimeType { get; }
    public RenderEngineType DefaultEngineType { get; }

    private TemplateFormat(int id, string name, string mimeType, RenderEngineType defaultEngineType) : base(id, name)
    {
        Extension = $".{name}";
        MimeType = mimeType;
        DefaultEngineType = defaultEngineType;
    }

    public static bool TryFromExtension(string? extension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TemplateFormat? format)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            format = null;
            return false;
        }

        string normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        return TryFromDisplayName(normalized, out format);
    }
}


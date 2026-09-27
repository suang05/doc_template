using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class OutputFormat : Enumeration
{
    public static readonly OutputFormat Pdf = new(1, "pdf");
    public static readonly OutputFormat Docx = new(2, "docx");
    public static readonly OutputFormat Xlsx = new(3, "xlsx");

    public string Extension { get; }
    public string MimeType { get; }

    private OutputFormat(int id, string name) : base(id, name)
    {
        Extension = name; // Without dot, matches ext
        MimeType = name switch
        {
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/pdf"
        };
    }
}

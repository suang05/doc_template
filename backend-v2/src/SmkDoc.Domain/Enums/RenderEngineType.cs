using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class RenderEngineType : Enumeration
{
    public static readonly RenderEngineType Html = new(1, "Html");
    public static readonly RenderEngineType Docx = new(2, "Docx");
    public static readonly RenderEngineType Excel = new(3, "Excel");

    private RenderEngineType(int id, string name) : base(id, name) { }
}

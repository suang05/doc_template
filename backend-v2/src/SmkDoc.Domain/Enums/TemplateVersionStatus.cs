using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class TemplateVersionStatus : Enumeration
{
    public static readonly TemplateVersionStatus Draft = new(0, "Draft");
    public static readonly TemplateVersionStatus Published = new(1, "Published");
    public static readonly TemplateVersionStatus Archived = new(2, "Archived");

    private TemplateVersionStatus(int id, string name) : base(id, name) { }
}

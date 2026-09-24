using System.Runtime.Serialization;

namespace SmkDoc.Domain.Enums;

public enum TemplateFormat
{
    [EnumMember(Value = "html")]
    Html = 1,

    [EnumMember(Value = "docx")]
    Docx = 2,

    [EnumMember(Value = "xlsx")]
    Xlsx = 3,

    [EnumMember(Value = "pdf")]
    Pdf = 4
}

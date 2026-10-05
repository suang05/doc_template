using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates;

/// <summary>
/// Legacy wrapper for ValidateTemplateHtmlUseCase. Retained for backwards compatibility.
/// </summary>
[Obsolete("Use SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml.ValidateTemplateHtmlUseCase instead.")]
public class TemplateValidateUseCase(IPdfRenderer pdfRenderer) 
    : Queries.ValidateTemplateHtml.ValidateTemplateHtmlUseCase(pdfRenderer)
{
}

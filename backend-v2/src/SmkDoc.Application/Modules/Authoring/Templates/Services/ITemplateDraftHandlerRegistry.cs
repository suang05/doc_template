using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Authoring.Templates.Services;

/// <summary>
/// Registry resolving the appropriate format-specific draft handler by TemplateFormat or file extension.
/// </summary>
public interface ITemplateDraftHandlerRegistry
{
    ITemplateDraftHandler GetHandler(TemplateFormat format);

    ITemplateDraftHandler GetHandler(string fileExtension);
}


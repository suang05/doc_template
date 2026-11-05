using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

/// <summary>
/// Command for persisting updated HTML code and page layout settings from Monaco Studio.
/// </summary>
public sealed record SaveTemplateHtmlCommand(
    Guid TemplateId,
    string Html,
    string? SamplePayload = null,
    string? ChangeNote = null,
    string? IfMatch = null,
    TemplatePageSettings? PageSettings = null
);

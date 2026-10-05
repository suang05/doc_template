namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

/// <summary>
/// Command for persisting updated HTML code from Monaco Studio.
/// </summary>
public record SaveTemplateHtmlCommand(
    string Html,
    string? SamplePayload = null,
    string? ChangeNote = null
);

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed record CreateTemplateCommand(
    string Name,
    string Slug,
    string? Category,
    Stream? FileStream = null,
    string? FileName = null
);

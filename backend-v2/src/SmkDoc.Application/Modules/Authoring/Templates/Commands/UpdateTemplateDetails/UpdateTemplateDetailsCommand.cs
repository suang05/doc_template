namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;

public sealed record UpdateTemplateDetailsCommand(
    Guid TemplateId,
    string Name,
    string? Category
);

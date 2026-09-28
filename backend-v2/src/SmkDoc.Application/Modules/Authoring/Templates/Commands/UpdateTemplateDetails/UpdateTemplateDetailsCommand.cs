namespace SmkDoc.Application.UseCases.Templates.Commands.UpdateTemplateDetails;

public sealed record UpdateTemplateDetailsCommand(
    Guid TemplateId,
    string Name,
    string? Category
);

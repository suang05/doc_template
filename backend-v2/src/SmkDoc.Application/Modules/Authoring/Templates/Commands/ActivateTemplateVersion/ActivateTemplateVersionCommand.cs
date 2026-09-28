namespace SmkDoc.Application.UseCases.Templates.Commands.ActivateTemplateVersion;

public sealed record ActivateTemplateVersionCommand(
    Guid TemplateId,
    Guid VersionId
);

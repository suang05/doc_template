namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;

public sealed record ActivateTemplateVersionCommand(
    Guid TemplateId,
    Guid VersionId
);

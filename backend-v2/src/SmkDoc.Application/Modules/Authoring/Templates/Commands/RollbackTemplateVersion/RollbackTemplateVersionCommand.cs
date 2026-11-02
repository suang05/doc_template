namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

public sealed record RollbackTemplateVersionCommand(Guid TemplateId, int TargetVersion);

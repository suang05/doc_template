using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public record CommitTemplateDraftCommand(string DraftId, CommitDraftCommand Request);

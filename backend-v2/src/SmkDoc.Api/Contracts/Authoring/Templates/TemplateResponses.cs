namespace SmkDoc.Api.Contracts.Authoring.Templates;

public record ParseDraftResponse(string DraftId, IEnumerable<string> Placeholders);

public record CommitDraftResponse(Guid TemplateId);

public record SaveHtmlResponse(int Version);

public record ScanFieldsResponse(IEnumerable<string> Placeholders);

public record RollbackVersionResponse(int Version);

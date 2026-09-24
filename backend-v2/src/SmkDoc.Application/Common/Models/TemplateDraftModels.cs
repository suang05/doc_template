namespace SmkDoc.Application.Common.Models;

public record TemplateDraftEntry(
    byte[] FileBytes,
    string FileName,
    string FileExtension,
    IReadOnlyList<string> Placeholders
);

public record ParseDraftResult(
    string DraftId,
    IReadOnlyList<string> Placeholders
);

public record PreviewDraftRequest(string DataJson);

public record CommitDraftRequest(
    string Name,
    string Slug,
    string? Category,
    IReadOnlyList<SaveFieldMappingItem> Mappings
);

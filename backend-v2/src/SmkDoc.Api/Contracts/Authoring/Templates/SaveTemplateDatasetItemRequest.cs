namespace SmkDoc.Api.Contracts.Authoring.Templates;

public record SaveTemplateDatasetItemRequest(Guid DatasetId, string Alias, int SortOrder);

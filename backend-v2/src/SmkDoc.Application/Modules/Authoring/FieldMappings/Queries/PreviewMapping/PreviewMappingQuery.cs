using System.Text.Json;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

public sealed record PreviewMappingQuery(
    Guid TemplateId,
    JsonElement SampleData
);

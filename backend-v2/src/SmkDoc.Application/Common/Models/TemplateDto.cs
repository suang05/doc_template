using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Common.Models;

public record TemplateDto(
    Guid Id,
    string Name,
    string Slug,
    string? Category,
    bool IsActive,
    Guid? CurrentVersionId,
    TemplateFormat? FileFormat,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record CreateTemplateRequest(
    string Name,
    string Slug,
    string? Category,
    string? HtmlContent
);

public record UpdateTemplateRequest(
    string? Name,
    string? Category,
    bool? IsActive
);

public record SaveTemplateHtmlRequest(
    string Html,
    string? SamplePayload = null,
    string? ChangeNote = null
);

public record TemplateStudioDto(
    string Html,
    string? SamplePayload,
    string? DataSchema,
    int Version
);

public record TemplateSchemaDto(
    Guid TemplateId,
    string Slug,
    TemplateFormat? Format,
    int Version,
    string? DataSchema,
    string? SamplePayload
);

public record TemplateValidationResult(
    bool Valid,
    List<string> Fields,
    List<string> Errors
);


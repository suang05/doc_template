using SmkDoc.Application.DTOs.Projects;

namespace SmkDoc.Api.Models;

/// <summary>Request model for creating a new project.</summary>
public record CreateProjectRequest(string Name, string Slug) : CreateProjectCommand(Name, Slug);

/// <summary>Request model for creating a new API key.</summary>
public record CreateApiKeyRequest(
    string Name,
    string CallerApp,
    string[]? Scopes,
    DateTimeOffset? ExpiresAt);

public record ProjectDto(Guid Id, string Name, string Slug, bool IsActive, DateTimeOffset CreatedAt) 
    : ProjectResultDto(Id, Name, Slug, IsActive, CreatedAt);
public record ProjectListItemDto(Guid Id, string Name, string Slug, bool IsActive, DateTimeOffset CreatedAt) 
    : ProjectResultDto(Id, Name, Slug, IsActive, CreatedAt);
public record ApiKeyResponseDto(Guid Id, string Name, string CallerApp, string Key, DateTimeOffset? ExpiresAt);
public record UploadFontResponse(string Message, string FontName);
public record TestConnectionResponse(string Message);
public record UserProfileDto(string Id, string Email, string FirstName, string LastName, string ProjectId, string Role);
public record ValidatePayloadResponse(bool Valid, string TemplateSlug, int SchemaVersion, IEnumerable<ValidationErrorDto> Errors);
public record ValidationErrorDto(string Path, string Message, string? Rule);
public record DownloadUrlResponse(string Url, int ExpiresInSeconds);
public record ParseDraftResponse(string DraftId, IEnumerable<string> Placeholders);
public record CommitDraftResponse(Guid TemplateId);
public record SaveHtmlResponse(int Version);
public record ScanFieldsResponse(IEnumerable<string> Placeholders);
public record RollbackVersionResponse(int Version);

using System.Diagnostics;
using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

/// <summary>
/// Single-responsibility Use Case for creating a new template and its initial version.
/// Strictly follows Clean Architecture (DIP, Encapsulation, zero framework leaks).
/// </summary>
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IDocxSecurityScanner securityScanner,
    ICurrentProjectContext projectContext,
    IUnitOfWork unitOfWork,
    ISchemaInferenceService schemaInferenceService,
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    private const long MaxTemplateFileSizeBytes = 20L * 1024 * 1024;

    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // Step 1: Parse & Guard (Fail-fast at line 1 via Smart Context Property)
        var projectId = projectContext.ProjectId;

        if (await templateRepo.SlugExistsAsync(command.Slug, ct))
        {
            throw ConflictException.DuplicateSlug(command.Slug);
        }

        var now = timeProvider.GetUtcNow();
        var template = Template.Create(
            projectId,
            TemplateName.Create(command.Name),
            TemplateSlug.Create(command.Slug),
            command.Category?.Trim(),
            now);

        var (storageKey, fileFormat, htmlContent) = await PrepareTemplateAssetAsync(command, template, ct);

        var initialVersion = template.CreateNextVersion(
            storageKey,
            fileFormat,
            projectContext.CallerIdentity ?? "system",
            now,
            "Initial version");

        PopulateInitialVersionManifest(initialVersion, template, fileFormat, htmlContent, now);

        try
        {
            await templateRepo.AddAsync(template, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception primaryEx)
        {
            await CompensateStorageDeleteAsync(storageKey, primaryEx);
            throw;
        }

        return new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name,
            template.Slug,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            initialVersion.FileFormat?.Name,
            template.CreatedAt,
            template.UpdatedAt,
            template.GetConcurrencyToken()
        );
    }

    private async Task<(string StorageKey, TemplateFormat Format, string? HtmlContent)> PrepareTemplateAssetAsync(
        CreateTemplateCommand command,
        Template template,
        CancellationToken ct)
    {
        if (command.FileStream != null && !string.IsNullOrWhiteSpace(command.FileName))
        {
            string ext = Path.GetExtension(command.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext)) ext = ".html";

            if (!TemplateFormat.TryFromExtension(ext, out var format) || format == null)
            {
                throw new DomainValidationException(
                    $"Unsupported template file extension: '{ext}'. Allowed formats: .html, .docx, .xlsx");
            }

            var storageKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, 1, ext);

            if (command.FileStream.CanSeek && command.FileStream.Length > MaxTemplateFileSizeBytes)
            {
                throw new DomainValidationException(
                    $"Template file size ({command.FileStream.Length / (1024 * 1024)} MB) exceeds the 20 MB limit.");
            }

            await using var spooled = await HybridSpoolStream.SpoolFromAsync(command.FileStream, ct: ct);
            if (spooled.Length > MaxTemplateFileSizeBytes)
            {
                throw new DomainValidationException(
                    $"Template file size ({spooled.Length / (1024 * 1024)} MB) exceeds the 20 MB limit.");
            }

            if (ext == ".docx")
            {
                var scanResult = securityScanner.Scan(spooled);
                if (scanResult is { IsSafe: false })
                {
                    throw new DomainValidationException(
                        $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
                }
            }

            string? htmlContent = null;
            if (format == TemplateFormat.Html)
            {
                spooled.Position = 0;
                using var reader = new StreamReader(spooled, Encoding.UTF8, leaveOpen: true);
                htmlContent = await reader.ReadToEndAsync(ct);
            }

            spooled.Position = 0;
            await storageService.UploadAsync(StorageBuckets.Templates, storageKey, spooled, format.MimeType, ct);
            return (storageKey, format, htmlContent);
        }

        var defaultStorageKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, 1, ".html");
        const string defaultHtml = "<!DOCTYPE html>\n<html lang=\"th\">\n<head>\n  <meta charset=\"UTF-8\" />\n</head>\n<body>\n  <h2>Template</h2>\n</body>\n</html>";
        await using var htmlStream = await HybridSpoolStream.CreateFromUtf8StringAsync(defaultHtml, ct: ct);
        await storageService.UploadAsync(StorageBuckets.Templates, defaultStorageKey, htmlStream, "text/html; charset=utf-8", ct);
        return (defaultStorageKey, TemplateFormat.Html, defaultHtml);
    }

    private void PopulateInitialVersionManifest(
        TemplateVersion version,
        Template template,
        TemplateFormat format,
        string? htmlContent,
        DateTimeOffset now)
    {
        if (format == TemplateFormat.Html && !string.IsNullOrWhiteSpace(htmlContent))
        {
            var (schema, samplePayload) = schemaInferenceService.InferFromHtml(htmlContent, null, template.Slug);
            version.UpdateDataSchema(schema, samplePayload, now);
        }
        else
        {
            var schema = schemaInferenceService.InferSchemaFromPlaceholders([], templateSlug: template.Slug);
            var samplePayload = schemaInferenceService.GenerateDefaultSamplePayload([]);
            version.UpdateDataSchema(schema, samplePayload, now);
        }

        version.UpdateMappingsSnapshot(FieldMappingSnapshotSerializer.Serialize(template.FieldMappings), now);
    }

    private async Task CompensateStorageDeleteAsync(string storageKey, Exception primaryEx)
    {
        try
        {
            await storageService.DeleteAsync(StorageBuckets.Templates, storageKey, CancellationToken.None);
        }
        catch (Exception cleanupEx)
        {
            primaryEx.Data["OrphanedStorageKey"] = storageKey;
            primaryEx.Data["CompensatingDeleteError"] = cleanupEx.Message;
            Trace.TraceWarning(
                "Failed to delete orphaned template object '{0}' after database commit failure: {1}",
                storageKey,
                cleanupEx.Message);
        }
    }
}

using System.Diagnostics;
using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
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
    TimeProvider timeProvider) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Creating a template requires an active project context.");
        }

        // 1. Domain Rule Check (Slug uniqueness within active tenant)
        var slugExists = await templateRepo.SlugExistsAsync(command.Slug, ct);
        if (slugExists)
        {
            throw ConflictException.DuplicateSlug(command.Slug);
        }

        // 2. Domain Aggregate Instantiation (provides canonical TemplateId for immutable object storage key)
        var now = timeProvider.GetUtcNow();
        var template = Template.Create(
            projectContext.ProjectId,
            TemplateName.Create(command.Name),
            TemplateSlug.Create(command.Slug),
            command.Category?.Trim(),
            now);

        string storageKey;
        TemplateFormat fileFormat;

        // 3. Asset Security Scanning & Immutable Storage Upload
        if (command.FileStream != null && !string.IsNullOrWhiteSpace(command.FileName))
        {
            string ext = Path.GetExtension(command.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".html";
            }

            fileFormat = ext switch
            {
                ".docx" => TemplateFormat.Docx,
                ".xlsx" => TemplateFormat.Xlsx,
                _ => TemplateFormat.Html
            };

            storageKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, 1, ext);

            if (ext == ".docx")
            {
                await using var scanCopy = await HybridSpoolStream.SpoolFromAsync(command.FileStream, ct: ct);

                var scanResult = securityScanner.Scan(scanCopy);
                if (scanResult is { IsSafe: false })
                {
                    throw new DomainValidationException(
                        $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
                }

                scanCopy.Position = 0;
                await storageService.UploadAsync(StorageBuckets.Templates, storageKey, scanCopy, fileFormat.MimeType, ct);
            }
            else
            {
                if (command.FileStream.CanSeek)
                {
                    command.FileStream.Position = 0;
                }

                await storageService.UploadAsync(StorageBuckets.Templates, storageKey, command.FileStream, fileFormat.MimeType, ct);
            }
        }
        else
        {
            fileFormat = TemplateFormat.Html;
            storageKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, 1, ".html");
            const string defaultHtml = "<!DOCTYPE html>\n<html lang=\"th\">\n<head>\n  <meta charset=\"UTF-8\" />\n</head>\n<body>\n  <h2>Template</h2>\n</body>\n</html>";
            using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(defaultHtml));
            await storageService.UploadAsync(StorageBuckets.Templates, storageKey, htmlStream, "text/html; charset=utf-8", ct);
        }

        // 4. Append-Only Initial Version & Compensating Storage Cleanup on Persistence Failure
        var initialVersion = template.CreateDraftVersion(
            1,
            storageKey,
            fileFormat,
            projectContext.CallerIdentity ?? "system",
            now,
            "Initial version");

        template.PublishVersion(initialVersion, now);

        try
        {
            await templateRepo.AddAsync(template, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception primaryEx)
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
            template.UpdatedAt
        );
    }
}

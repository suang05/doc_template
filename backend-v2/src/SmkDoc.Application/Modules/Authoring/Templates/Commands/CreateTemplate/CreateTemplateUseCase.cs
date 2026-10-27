using System.Text;
using FluentValidation;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

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
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    IValidator<CreateTemplateCommand> validator,
    TimeProvider? timeProvider = null) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Fail-Fast Input Validation
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var effectiveProjectId = command.ProjectId != Guid.Empty
            ? command.ProjectId
            : (projectContext.IsResolved ? projectContext.ProjectId.Value : Guid.Empty);

        // 2. Domain Rule Check (Slug uniqueness)
        var slugExists = await templateRepo.SlugExistsAsync(command.Slug, ct);
        if (slugExists)
        {
            throw ConflictException.DuplicateSlug(command.Slug);
        }

        string storageKey;
        TemplateFormat fileFormat;

        // 3. Asset Storage & Security scanning
        if (command.FileStream != null && !string.IsNullOrWhiteSpace(command.FileName))
        {
            string ext = Path.GetExtension(command.FileName).ToLowerInvariant();

            if (ext == ".docx")
            {
                using var scanCopy = new MemoryStream();
                await command.FileStream.CopyToAsync(scanCopy, ct);
                scanCopy.Position = 0;
                command.FileStream.Position = 0;

                var scanResult = securityScanner.Scan(scanCopy);
                if (!scanResult.IsSafe)
                {
                    throw new DomainValidationException(
                        $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
                }
            }

            storageKey = $"templates/{effectiveProjectId}/{command.Slug}{ext}";
            fileFormat = ext switch
            {
                ".docx" => TemplateFormat.Docx,
                ".xlsx" => TemplateFormat.Xlsx,
                _       => TemplateFormat.Html
            };
            string contentType = fileFormat.MimeType;
            await storageService.UploadAsync(StorageBuckets.Templates, storageKey, command.FileStream, contentType, ct);
        }
        else
        {
            storageKey = $"templates/{effectiveProjectId}/{command.Slug}.html";
            fileFormat = TemplateFormat.Html;
            string defaultHtml = "<!DOCTYPE html>\n<html lang=\"th\">\n<head>\n  <meta charset=\"UTF-8\" />\n</head>\n<body>\n  <h2>Template</h2>\n</body>\n</html>";
            using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(defaultHtml));
            await storageService.UploadAsync(StorageBuckets.Templates, storageKey, htmlStream, "text/html; charset=utf-8", ct);
        }

        // 4. Domain Entity Instantiation (Encapsulated invariants)
        var now = this.timeProvider.GetUtcNow();
        var template = Template.Create(
            new ProjectId(effectiveProjectId),
            TemplateName.Create(command.Name),
            TemplateSlug.Create(command.Slug),
            command.Category?.Trim(),
            now);
        var initialVersion = template.CreateDraftVersion(
            1,
            storageKey,
            fileFormat,
            executionContext.CallerApp ?? "system",
            now,
            "Initial version");

        template.PublishVersion(initialVersion, now);

        await templateRepo.AddAsync(template, ct);
        await unitOfWork.CommitAsync(ct);

        // 5. Safe Result DTO Mapping
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

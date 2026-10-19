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

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

/// <summary>
/// Single-responsibility Use Case for creating a new template and its initial version.
/// Strictly follows Clean Architecture (DIP, Encapsulation, zero framework leaks).
/// </summary>
public sealed class CreateTemplateUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IDocxSecurityScanner securityScanner,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    IValidator<CreateTemplateCommand> validator,
    TimeProvider? timeProvider = null,
    ICurrentProjectContext? projectContext = null) : IUseCase<CreateTemplateCommand, TemplateResultDto>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IDocxSecurityScanner _securityScanner = securityScanner;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CreateTemplateCommand> _validator = validator;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ICurrentProjectContext? _projectContext = projectContext;

    public async Task<TemplateResultDto> ExecuteAsync(CreateTemplateCommand command, CancellationToken ct = default)
    {
        // 1. Fail-Fast Input Validation
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var effectiveProjectId = command.ProjectId != Guid.Empty
            ? command.ProjectId
            : (_projectContext?.IsResolved == true ? _projectContext.ProjectId.Value : _executionContext.ProjectId ?? Guid.Empty);

        // 2. Domain Rule Check (Slug uniqueness)
        var slugExists = await _templateRepo.SlugExistsAsync(command.Slug, ct);
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

                var scanResult = _securityScanner.Scan(scanCopy);
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
            await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, command.FileStream, contentType, ct);
        }
        else
        {
            storageKey = $"templates/{effectiveProjectId}/{command.Slug}.html";
            fileFormat = TemplateFormat.Html;
            string defaultHtml = "<!DOCTYPE html>\n<html lang=\"th\">\n<head>\n  <meta charset=\"UTF-8\" />\n</head>\n<body>\n  <h2>Template</h2>\n</body>\n</html>";
            using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(defaultHtml));
            await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, htmlStream, "text/html; charset=utf-8", ct);
        }

        // 4. Domain Entity Instantiation (Encapsulated invariants)
        var now = _timeProvider.GetUtcNow();
        var template = Template.Create(
            effectiveProjectId,
            TemplateName.Create(command.Name),
            TemplateSlug.Create(command.Slug),
            command.Category?.Trim(),
            now);
        var initialVersion = TemplateVersion.Draft(
            template.Id,
            1,
            storageKey,
            fileFormat,
            _executionContext.CallerApp ?? "system",
            now,
            "Initial version");

        initialVersion.Publish(now);
        template.SetCurrentVersion(initialVersion.Id, now);

        await _templateRepo.AddAsync(template, ct);
        await _templateRepo.AddVersionAsync(initialVersion, ct);
        await _unitOfWork.CommitAsync(ct);

        // 5. Safe Result DTO Mapping
        return new TemplateResultDto(
            template.Id,
            template.ProjectId,
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

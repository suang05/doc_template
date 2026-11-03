using System.Diagnostics;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents;

public sealed class GenerateDocumentUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IDocumentRenderPipeline renderPipeline,
    IDocumentAuditService auditService,
    IDocumentVersioningService versioningService,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>
{
    public async Task<GenerateDocumentResultDto> ExecuteAsync(
        GenerateDocumentCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Document generation requires an active project context.");
        }

        string slug = command.Slug;
        var template = await templateRepo.GetBySlugWithDetailsAsync(slug, ct);
        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{slug}' not found or inactive.");
        }

        var currentVersion = template.GetPublishedVersion();
        var outputFormat = command.OutputFormat.Trim().ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "xlsx" => OutputFormat.Xlsx,
            _ => OutputFormat.Pdf
        };

        var generationId = Guid.CreateVersion7();
        var tenantId = projectContext.ProjectId.Value;
        string ext = outputFormat.Extension;
        string outputKey = $"outputs/{tenantId:N}/{timeProvider.GetUtcNow():yyyy/MM/dd}/{slug}_{generationId:N}.{ext}";

        var renderResult = await renderPipeline.RenderAndUploadAsync(
            template, currentVersion, command.Payload, command.SkipValidation, outputFormat, outputKey, ct);

        try
        {
            var expiry = TimeSpan.FromHours(24);
            string downloadUrl = await storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, outputKey, expiry, ct);

            await auditService.LogSuccessAsync(
                generationId,
                template.Id.Value,
                currentVersion.Id,
                renderResult.PreparedDataJson,
                outputKey,
                outputFormat,
                renderResult.FileSizeBytes,
                renderResult.ElapsedMilliseconds,
                ct);

            if (!string.IsNullOrWhiteSpace(command.DocumentRef))
            {
                await versioningService.RecordVersionAsync(
                    command.DocumentRef,
                    template.Id.Value,
                    currentVersion.Id,
                    generationId,
                    command.ChangeNote,
                    ct);
            }

            await unitOfWork.CommitAsync(ct);

            return new GenerateDocumentResultDto(
                GenerationId: generationId,
                DocumentRef: command.DocumentRef,
                OutputFormat: ext,
                FileSizeBytes: renderResult.FileSizeBytes,
                Sha256: renderResult.Sha256Hex,
                Url: downloadUrl,
                ExpiresAt: timeProvider.GetUtcNow().Add(expiry)
            );
        }
        catch (Exception primaryEx)
        {
            try
            {
                await storageService.DeleteAsync(StorageBuckets.Outputs, outputKey, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                primaryEx.Data["OrphanedOutputKey"] = outputKey;
                primaryEx.Data["CompensatingDeleteError"] = cleanupEx.Message;
                Trace.TraceWarning(
                    "Failed to delete orphaned output object '{0}' after database commit failure: {1}",
                    outputKey,
                    cleanupEx.Message);
            }

            throw;
        }
    }
}

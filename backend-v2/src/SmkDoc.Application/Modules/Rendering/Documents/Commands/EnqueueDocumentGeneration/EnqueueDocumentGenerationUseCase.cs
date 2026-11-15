using System.Text.Json;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;

public sealed class EnqueueDocumentGenerationUseCase(
    ITemplateRepository templateRepo,
    IDocumentJobRepository documentJobRepo,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider,
    IQuotaReader? quotaReader = null) : IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto>
{
    public async Task<DocumentJobDto> ExecuteAsync(EnqueueDocumentGenerationCommand command, CancellationToken ct)
    {
        var projectId = projectContext.GetRequiredProjectId();

        var template = await templateRepo.GetBySlugWithDetailsAsync(command.Slug, ct);
        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{command.Slug}' not found or inactive.");
        }

        _ = template.GetPublishedVersion();

        if (quotaReader is not null)
        {
            var quota = await quotaReader.GetQuotaForProjectAsync(projectId, ct);
            if (quota.MaxBatchItems < 1)
            {
                throw new BusinessRuleViolationException("Project batch quota does not permit enqueuing document jobs.", ErrorCodes.QuotaExceeded);
            }
        }

        var now = timeProvider.GetUtcNow();
        var outputFormat = OutputFormat.FromDisplayName<OutputFormat>(command.OutputFormat.Trim());
        string payloadJson = command.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? "{}"
            : command.Payload.GetRawText();

        var job = DocumentJob.Create(
            projectId,
            template.Id,
            command.Slug,
            template.CurrentVersionId,
            projectContext.CallerIdentity ?? "m2m-client",
            outputFormat,
            [(payloadJson, command.DocumentRef)],
            now);

        await documentJobRepo.AddAsync(job, ct);
        await unitOfWork.CommitAsync(ct);

        return DocumentJobDto.FromDomainJob(job);
    }
}

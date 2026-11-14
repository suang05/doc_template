using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Models;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;

public sealed class EnqueueDocumentGenerationUseCase(
    ITemplateRepository templateRepo,
    IDocumentRenderQueue renderQueue,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto>
{
    public async Task<DocumentJobDto> ExecuteAsync(EnqueueDocumentGenerationCommand command, CancellationToken ct)
    {
        // Step 1: Parse & Guard (Fail-fast at line 1 via Smart Context Property)
        var projectId = projectContext.GetRequiredProjectId();

        var template = await templateRepo.GetBySlugWithDetailsAsync(command.Slug, ct);
        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{command.Slug}' not found or inactive.");
        }

        _ = template.GetPublishedVersion();

        var now = timeProvider.GetUtcNow();
        var jobId = Guid.CreateVersion7();
        string payloadJson = command.Payload.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null
            ? "{}"
            : command.Payload.GetRawText();

        var job = new DocumentJob(
            jobId,
            projectId.Value,
            projectContext.CallerIdentity ?? "m2m-client",
            command.Slug,
            payloadJson,
            command.OutputFormat,
            command.DocumentRef,
            command.ChangeNote,
            command.SkipValidation,
            now);

        await renderQueue.EnqueueAsync(job, ct);

        return DocumentJobDto.FromJob(job);
    }
}


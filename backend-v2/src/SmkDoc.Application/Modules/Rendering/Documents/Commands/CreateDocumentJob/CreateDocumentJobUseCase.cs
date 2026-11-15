using System.Text.Json;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.CreateDocumentJob;

public sealed class CreateDocumentJobUseCase(
    ITemplateRepository templateRepo,
    IDocumentJobRepository documentJobRepo,
    IQuotaReader quotaReader,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider,
    IConnectionHostValidator? connectionHostValidator = null) : IUseCase<CreateDocumentJobCommand, DocumentJobResultDto>
{
    public async Task<DocumentJobResultDto> ExecuteAsync(CreateDocumentJobCommand command, CancellationToken ct)
    {
        var projectId = projectContext.GetRequiredProjectId();

        var template = await templateRepo.GetBySlugWithDetailsAsync(command.Slug, ct);
        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{command.Slug}' not found or inactive.");
        }

        _ = template.GetPublishedVersion();

        var quota = await quotaReader.GetQuotaForProjectAsync(projectId, ct);
        if (command.Items.Count > quota.MaxBatchItems)
        {
            throw new BusinessRuleViolationException(
                $"Batch item count ({command.Items.Count}) exceeds project quota of {quota.MaxBatchItems} items.",
                ErrorCodes.QuotaExceeded);
        }

        var webhookUri = await ValidateWebhookUriAsync(command.WebhookUrl, projectId, ct);

        var items = command.Items
            .Select(item => (
                PayloadJson: item.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? "{}"
                    : item.Payload.GetRawText(),
                item.DocumentRef))
            .ToArray();

        var now = timeProvider.GetUtcNow();
        var outputFormat = OutputFormat.FromDisplayName<OutputFormat>(command.OutputFormat.Trim());

        var job = DocumentJob.Create(
            projectId,
            template.Id,
            command.Slug,
            template.CurrentVersionId,
            projectContext.CallerIdentity ?? "m2m-client",
            outputFormat,
            items,
            now,
            webhookUri,
            command.IdempotencyKey,
            command.RequestFingerprint,
            command.MaxAttempts);

        await documentJobRepo.AddAsync(job, ct);
        await unitOfWork.CommitAsync(ct);

        return DocumentJobResultMapper.ToDto(job);
    }

    private async Task<Uri?> ValidateWebhookUriAsync(string? webhookUrl, ProjectId projectId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(webhookUrl.Trim(), UriKind.Absolute, out var webhookUri) ||
            (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("WebhookUrl", "WebhookUrl must be an absolute HTTP or HTTPS URI.");
        }

        if (connectionHostValidator is not null)
        {
            int port = ResolvePort(webhookUri);
            await connectionHostValidator.ResolveAndValidateHostAsync(webhookUri.Host, port, projectId, ct);
        }

        return webhookUri;
    }

    private static int ResolvePort(Uri uri)
    {
        if (!uri.IsDefaultPort)
        {
            return uri.Port;
        }

        return uri.Scheme == Uri.UriSchemeHttps ? 443 : 80;
    }
}

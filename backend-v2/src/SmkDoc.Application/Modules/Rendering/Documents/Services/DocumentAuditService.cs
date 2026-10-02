using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditService(
    IRepository<GenerationLog> logRepo,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    IDocumentMetrics? metrics = null) : IDocumentAuditService
{
    private readonly IRepository<GenerationLog> _logRepo = logRepo;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDocumentMetrics? _metrics = metrics;

    public async Task LogValidationFailureAsync(
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        OutputFormat outputFormat,
        int elapsedMs,
        string errorMessage,
        CancellationToken ct = default)
    {
        var failLog = new GenerationLog(
            templateId,
            templateVersionId,
            _executionContext.ApiKeyId,
            _executionContext.CallerApp,
            "api",
            dataJson,
            null,
            outputFormat,
            0,
            null,
            null,
            elapsedMs,
            "VALIDATION_FAILED",
            errorMessage);

        await _logRepo.AddAsync(failLog, ct);
        await _unitOfWork.CommitAsync(ct);

        _metrics?.RecordGenerationFailure("Unknown", outputFormat.Extension, elapsedMs, "VALIDATION_FAILED");
    }

    public async Task LogSuccessAsync(
        Guid generationId,
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        string outputKey,
        OutputFormat outputFormat,
        long fileSizeBytes,
        int elapsedMs,
        CancellationToken ct = default)
    {
        var log = new GenerationLog(
            templateId,
            templateVersionId,
            _executionContext.ApiKeyId,
            _executionContext.CallerApp,
            "api",
            dataJson,
            outputKey,
            outputFormat,
            fileSizeBytes,
            null,
            null,
            elapsedMs,
            "SUCCESS",
            null)
        {
            Id = generationId
        };

        await _logRepo.AddAsync(log, ct);

        _metrics?.RecordGenerationSuccess("Engine", outputFormat.Extension, elapsedMs, fileSizeBytes);
    }
}

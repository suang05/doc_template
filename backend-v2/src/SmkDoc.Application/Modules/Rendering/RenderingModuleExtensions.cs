using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.CancelDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.CreateDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RetryDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJobResult;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJobStatus;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobItems;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogPayload;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Rendering;

public static class RenderingModuleExtensions
{
    public static IServiceCollection AddRenderingModule(this IServiceCollection services)
    {
        // Sub-Domain Collaborator Services
        services.AddScoped<IDocumentAuditProcessor, DocumentAuditProcessor>();
        services.AddScoped<IDocumentVersionProcessor, DocumentVersionProcessor>();
        services.AddScoped<IDocumentRetentionProcessor, DocumentRetentionProcessor>();

        // Domain Event Handlers
        services.AddScoped<IDomainEventHandler<DocumentVersionCreatedDomainEvent>, DocumentVersionCreatedEventHandler>();
        services.AddScoped<IDomainEventHandler<DocumentJobSucceededDomainEvent>, DocumentJobSucceededEventHandler>();
        services.AddScoped<IDomainEventHandler<DocumentJobFailedDomainEvent>, DocumentJobFailedEventHandler>();
        services.AddScoped<IDomainEventHandler<DocumentJobCanceledDomainEvent>, DocumentJobCanceledEventHandler>();

        // High-Throughput & Stateless Document Generation Pipeline (Action-Centric Vertical Slice)
        services.AddUseCase<GenerateDocumentCommand, GenerateDocumentResultDto, GenerateDocumentUseCase>();
        services.AddUseCase<PreviewDocumentQuery, Stream, PreviewDocumentUseCase>();
        services.AddUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>, GetDocumentVersionsUseCase>();
        services.AddUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult, DownloadDocumentVersionUseCase>();

        // Observability & Generation Logs (Action-Centric Vertical Slice)
        services.AddUseCase<ListGenerationLogsQuery, GenerationLogPagedResult, ListGenerationLogsUseCase>();
        services.AddUseCase<GetLogMetricsQuery, LogMetricsDto, GetLogMetricsUseCase>();
        services.AddUseCase<GetLogDownloadUrlQuery, DownloadUrlResultDto, GetLogDownloadUrlUseCase>();
        services.AddUseCase<GetLogPayloadQuery, LogPayloadResultDto, GetLogPayloadUseCase>();
        services.AddUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult, DownloadGenerationLogDocumentUseCase>();

        // Legacy Async Job Queue Compatibility (A1 Coexistence)
        services.AddUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto, EnqueueDocumentGenerationUseCase>();
        services.AddUseCase<GetDocumentJobQuery, DocumentJobDto, GetDocumentJobUseCase>();

        // Durable PostgreSQL Pipeline C Document Jobs
        services.AddUseCase<CreateDocumentJobCommand, DocumentJobResultDto, CreateDocumentJobUseCase>();
        services.AddUseCase<GetDocumentJobStatusQuery, DocumentJobResultDto, GetDocumentJobStatusUseCase>();
        services.AddUseCase<ListDocumentJobsQuery, DocumentJobListResult, ListDocumentJobsUseCase>();
        services.AddUseCase<GetDocumentJobResultQuery, DocumentJobFileResult, GetDocumentJobResultUseCase>();
        services.AddUseCase<ListDocumentJobItemsQuery, IReadOnlyList<DocumentJobItemDto>, ListDocumentJobItemsUseCase>();
        services.AddUseCase<CancelDocumentJobCommand, DocumentJobResultDto, CancelDocumentJobUseCase>();
        services.AddUseCase<RetryDocumentJobCommand, DocumentJobResultDto, RetryDocumentJobUseCase>();

        return services;
    }
}

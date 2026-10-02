using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

namespace SmkDoc.Application.Modules.Rendering;

public static class RenderingModuleExtensions
{
    public static IServiceCollection AddRenderingModule(this IServiceCollection services)
    {
        // Sub-Domain Collaborator Services
        services.AddScoped<IDocumentDataPreparationService, DocumentDataPreparationService>();
        services.AddScoped<IDocumentAuditService, DocumentAuditService>();
        services.AddScoped<IDocumentVersioningService, DocumentVersioningService>();

        // High-Throughput & Stateless Document Generation Pipeline (Action-Centric Vertical Slice)
        services.AddScoped<GenerateDocumentUseCase>();
        services.AddScoped<PreviewDocumentUseCase>();
        services.AddScoped<RenderStatelessDocumentUseCase>();
        services.AddScoped<HtmlToPdfUseCase>();
        services.AddScoped<GetDocumentVersionsUseCase>();
        services.AddScoped<DownloadDocumentVersionUseCase>();

        // Observability & Generation Logs (Action-Centric Vertical Slice)
        services.AddScoped<ListGenerationLogsUseCase>();
        services.AddScoped<GetLogMetricsUseCase>();
        services.AddScoped<GetLogDownloadUrlUseCase>();

        return services;
    }
}

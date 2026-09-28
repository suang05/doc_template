using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Application.Modules.Rendering.Logs;

namespace SmkDoc.Application.Modules.Rendering;

public static class RenderingModuleExtensions
{
    public static IServiceCollection AddRenderingModule(this IServiceCollection services)
    {
        // Sub-Domain Collaborator Services
        services.AddScoped<IDocumentDataPreparationService, DocumentDataPreparationService>();
        services.AddScoped<IDocumentAuditService, DocumentAuditService>();
        services.AddScoped<IDocumentVersioningService, DocumentVersioningService>();

        // High-Throughput & Stateless Document Generation Pipeline
        services.AddScoped<GenerateDocumentUseCase>();
        services.AddScoped<PreviewDocumentUseCase>();
        services.AddScoped<DocumentVersionUseCase>();
        services.AddScoped<RenderStatelessDocumentUseCase>();
        services.AddScoped<HtmlToPdfUseCase>();

        // Observability & Generation Logs
        services.AddScoped<GenerationLogUseCase>();

        return services;
    }
}

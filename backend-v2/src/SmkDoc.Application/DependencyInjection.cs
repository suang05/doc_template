using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring;
using SmkDoc.Application.Modules.IdentityAccess;
using SmkDoc.Application.Modules.Integration;
using SmkDoc.Application.Modules.Rendering;
using SmkDoc.Application.Modules.Rendering.Documents.Validators;

namespace SmkDoc.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Shared Kernel & Helpers
        services.AddSingleton<IMathExpressionResolver, MathExpressionResolverService>();
        services.AddScoped<IFieldMappingApplicatorService, FieldMappingApplicatorService>();

        // 1. Rendering Bounded Context (Stateless Document Generation, Logs)
        services.AddRenderingModule();

        // 2. Authoring Bounded Context (Templates, Studio, FieldMappings, Fonts, Schemas)
        services.AddAuthoringModule();

        // 3. Integration Bounded Context (DataConnections, Datasets)
        services.AddIntegrationModule();

        // 4. IdentityAccess Bounded Context (Users, Projects, Security/Auth, ApiKeys)
        services.AddIdentityAccessModule();

        // Register all FluentValidation validators in this assembly
        services.AddValidatorsFromAssemblyContaining<GenerateDocumentCommandValidator>();

        return services;
    }
}

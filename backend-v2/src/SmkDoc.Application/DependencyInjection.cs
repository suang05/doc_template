using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Validators.Documents;
using SmkDoc.Application.UseCases.DataConnections;
using SmkDoc.Application.UseCases.Datasets;
using SmkDoc.Application.UseCases.Documents;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Application.UseCases.Fonts;
using SmkDoc.Application.UseCases.Logs;
using SmkDoc.Application.UseCases.Projects;
using SmkDoc.Application.UseCases.Security;
using SmkDoc.Application.UseCases.Templates;

namespace SmkDoc.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMathExpressionResolver, MathExpressionResolverService>();
        services.AddScoped<IFieldMappingApplicatorService, FieldMappingApplicatorService>();

        services.AddScoped<GenerateDocumentUseCase>();
        services.AddScoped<ValidatePayloadUseCase>();
        services.AddScoped<PreviewDocumentUseCase>();
        services.AddScoped<DocumentVersionUseCase>();
        services.AddScoped<RenderStatelessDocumentUseCase>();
        services.AddScoped<HtmlToPdfUseCase>();
        services.AddScoped<TemplateManagementUseCase>();
        services.AddScoped<IHtmlStudioUseCase, HtmlStudioUseCase>();
        services.AddScoped<IHtmlPersistenceUseCase, HtmlPersistenceUseCase>();
        services.AddScoped<TemplateValidateUseCase>();
        services.AddScoped<ValidateTemplatePayloadUseCase>();
        services.AddScoped<SmkDoc.Application.UseCases.Schemas.ValidateStandaloneSchemaUseCase>();
        services.AddScoped<FieldMappingUseCase>();
        services.AddScoped<PreviewMappingUseCase>();
        services.AddScoped<TemplateDatasetUseCase>();
        services.AddScoped<TemplateDraftUseCase>();
        services.AddScoped<ApiKeyUseCase>();
        services.AddScoped<DataConnectionUseCase>();
        services.AddScoped<DatasetUseCase>();
        services.AddScoped<GenerationLogUseCase>();
        services.AddScoped<ProjectManagementUseCase>();
        services.AddScoped<FontManagementUseCase>();
        
        services.AddScoped<LoginUseCase>();
        services.AddScoped<UserManagementUseCase>();

        // Register all FluentValidation validators in this assembly
        services.AddValidatorsFromAssemblyContaining<GenerateDocumentCommandValidator>();

        return services;
    }
}

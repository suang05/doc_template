using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.FieldMappings;
using SmkDoc.Application.Modules.Authoring.Fonts;
using SmkDoc.Application.Modules.Authoring.Schemas;

namespace SmkDoc.Application.Modules.Authoring;

public static class AuthoringModuleExtensions
{
    public static IServiceCollection AddAuthoringModule(this IServiceCollection services)
    {
        // Template Lifecycle Commands
        services.AddScoped<CreateTemplateUseCase>();
        services.AddScoped<UpdateTemplateDetailsUseCase>();
        services.AddScoped<ActivateTemplateVersionUseCase>();
        services.AddScoped<DeactivateTemplateUseCase>();
        services.AddScoped<RollbackTemplateVersionUseCase>();

        // Template Queries
        services.AddScoped<GetTemplateByIdUseCase>();
        services.AddScoped<ListTemplatesUseCase>();
        services.AddScoped<ListTemplateVersionsUseCase>();
        services.AddScoped<DownloadTemplateUseCase>();
        services.AddScoped<ScanTemplatePlaceholdersUseCase>();

        // Monaco Studio & Drafts
        services.AddScoped<IHtmlStudioUseCase, HtmlStudioUseCase>();
        services.AddScoped<IHtmlPersistenceUseCase, HtmlPersistenceUseCase>();
        services.AddScoped<TemplateDraftUseCase>();
        services.AddScoped<TemplateValidateUseCase>();
        services.AddScoped<ValidateTemplatePayloadUseCase>();

        // Field Mappings & Datasets
        services.AddScoped<FieldMappingUseCase>();
        services.AddScoped<PreviewMappingUseCase>();
        services.AddScoped<TemplateDatasetUseCase>();

        // Fonts
        services.AddScoped<FontManagementUseCase>();

        // Schemas
        services.AddScoped<ValidateStandaloneSchemaUseCase>();

        return services;
    }
}

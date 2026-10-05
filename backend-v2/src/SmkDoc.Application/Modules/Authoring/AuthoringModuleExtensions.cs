using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.Authoring.FieldMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Application.Modules.Authoring.Schemas;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;

namespace SmkDoc.Application.Modules.Authoring;

public static class AuthoringModuleExtensions
{
    public static IServiceCollection AddAuthoringModule(this IServiceCollection services)
    {
        // Template Lifecycle Commands (Action-Centric Vertical Slice)
        services.AddScoped<CreateTemplateUseCase>();
        services.AddScoped<UpdateTemplateDetailsUseCase>();
        services.AddScoped<ActivateTemplateVersionUseCase>();
        services.AddScoped<DeactivateTemplateUseCase>();
        services.AddScoped<RollbackTemplateVersionUseCase>();

        // Template Queries (Action-Centric Vertical Slice)
        services.AddScoped<GetTemplateByIdUseCase>();
        services.AddScoped<ListTemplatesUseCase>();
        services.AddScoped<ListTemplateVersionsUseCase>();
        services.AddScoped<DownloadTemplateUseCase>();
        services.AddScoped<ScanTemplatePlaceholdersUseCase>();
        services.AddScoped<ScanUploadedTemplateUseCase>();

        // Monaco Studio & Draft Pipeline (Action-Centric Vertical Slice)
        services.AddScoped<IHtmlStudioUseCase, HtmlStudioUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml.SaveTemplateHtmlUseCase>();
        services.AddScoped<IHtmlPersistenceUseCase>(sp => sp.GetRequiredService<SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml.SaveTemplateHtmlUseCase>());
        services.AddScoped<ParseTemplateDraftUseCase>();
        services.AddScoped<PreviewTemplateDraftUseCase>();
        services.AddScoped<CommitTemplateDraftUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml.ValidateTemplateHtmlUseCase>();
        services.AddScoped<SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload.ValidateTemplatePayloadUseCase>();
#pragma warning disable CS0618
        services.AddScoped<HtmlPersistenceUseCase>();
        services.AddScoped<TemplateValidateUseCase>();
        services.AddScoped<ValidateTemplatePayloadUseCase>();
#pragma warning restore CS0618

        // Field Mappings & Datasets (Action-Centric Vertical Slice)
        services.AddScoped<SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping.PreviewMappingUseCase>();
#pragma warning disable CS0618
        services.AddScoped<PreviewMappingUseCase>();
        services.AddScoped<TemplateDatasetUseCase>();
#pragma warning restore CS0618
        services.AddScoped<GetTemplateMappingsUseCase>();
        services.AddScoped<SaveTemplateMappingsUseCase>();
        services.AddScoped<GetTemplateDatasetsUseCase>();
        services.AddScoped<SaveTemplateDatasetsUseCase>();

        // Fonts (Action-Centric Vertical Slice)
        services.AddScoped<ListFontsUseCase>();
        services.AddScoped<UploadFontUseCase>();
        services.AddScoped<GetFontBase64UseCase>();

        // Schemas
        services.AddScoped<ValidateStandaloneSchemaUseCase>();

        return services;
    }
}

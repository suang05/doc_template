using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Application.Modules.Authoring.Schemas.Commands.ValidateStandaloneSchema;
using SmkDoc.Application.Modules.Authoring.Schemas.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.EventHandlers;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.Authoring;

public static class AuthoringModuleExtensions
{
    public static IServiceCollection AddAuthoringModule(this IServiceCollection services)
    {
        // Domain Event Handlers
        services.AddScoped<IDomainEventHandler<TemplatePublishedDomainEvent>, TemplatePublishedEventHandler>();

        // Template Lifecycle Commands (Action-Centric Vertical Slice)
        services.AddUseCase<CreateTemplateCommand, TemplateResultDto, CreateTemplateUseCase>();
        services.AddUseCase<UpdateTemplateDetailsCommand, TemplateResultDto, UpdateTemplateDetailsUseCase>();
        services.AddUseCase<DeactivateTemplateCommand, DeactivateTemplateUseCase>();
        services.AddUseCase<RollbackTemplateVersionCommand, RollbackVersionResultDto, RollbackTemplateVersionUseCase>();

        // Template Queries (Action-Centric Vertical Slice)
        services.AddUseCase<GetTemplateByIdQuery, TemplateResultDto, GetTemplateByIdUseCase>();
        services.AddUseCase<ListTemplatesQuery, TemplatePagedResultDto, ListTemplatesUseCase>();
        services.AddUseCase<ListTemplateVersionsQuery, List<TemplateVersionDto>, ListTemplateVersionsUseCase>();
        services.AddUseCase<DownloadTemplateQuery, DownloadTemplateResult, DownloadTemplateUseCase>();
        services.AddUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto, ScanTemplatePlaceholdersUseCase>();
        services.AddUseCase<ScanUploadedTemplateQuery, ScanFieldsResultDto, ScanUploadedTemplateUseCase>();

        // Monaco Studio & Draft Pipeline (Action-Centric Vertical Slice)
        services.AddUseCase<GetTemplateHtmlSourceQuery, string, GetTemplateHtmlSourceUseCase>();
        services.AddUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto, GetTemplateStudioBundleUseCase>();
        services.AddUseCase<GetTemplateSchemaQuery, TemplateSchemaDto, GetTemplateSchemaUseCase>();
        services.AddUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto, SaveTemplateHtmlUseCase>();
        services.AddUseCase<ParseTemplateDraftCommand, ParseDraftResultDto, ParseTemplateDraftUseCase>();
        services.AddUseCase<PreviewTemplateDraftQuery, Stream, PreviewTemplateDraftUseCase>();
        services.AddUseCase<CommitTemplateDraftCommand, CommitDraftResultDto, CommitTemplateDraftUseCase>();
        services.AddUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto, ValidateTemplateHtmlUseCase>();
        services.AddUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult, ValidateTemplatePayloadUseCase>();

        // Field Mappings & Datasets (Action-Centric Vertical Slice)
        services.AddUseCase<PreviewMappingQuery, Stream, PreviewMappingUseCase>();
        services.AddUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>, GetTemplateMappingsUseCase>();
        services.AddUseCase<SaveTemplateMappingsCommand, List<FieldMappingDto>, SaveTemplateMappingsUseCase>();
        services.AddUseCase<GetTemplateDatasetsQuery, List<TemplateDatasetDto>, GetTemplateDatasetsUseCase>();
        services.AddUseCase<SaveTemplateDatasetsCommand, List<TemplateDatasetDto>, SaveTemplateDatasetsUseCase>();

        // Fonts (Action-Centric Vertical Slice)
        services.AddUseCase<ListFontsQuery, List<string>, ListFontsUseCase>();
        services.AddUseCase<GetFontCatalogQuery, FontCatalogDto, GetFontCatalogUseCase>();
        services.AddUseCase<UploadFontCommand, UploadFontResultDto, UploadFontUseCase>();
        services.AddUseCase<GetFontBase64Query, string, GetFontBase64UseCase>();

        // Schemas
        services.AddUseCase<ValidateStandaloneSchemaCommand, ValidateStandaloneSchemaResult, ValidateStandaloneSchemaUseCase>();

        return services;
    }
}

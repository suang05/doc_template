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
using SmkDoc.Application.UseCases.Templates.Commands.CreateTemplate;
using SmkDoc.Application.UseCases.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.UseCases.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Application.UseCases.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.UseCases.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.UseCases.Templates.Queries.GetTemplateById;
using SmkDoc.Application.UseCases.Templates.Queries.ListTemplates;
using SmkDoc.Application.UseCases.Templates.Queries.ListTemplateVersions;
using SmkDoc.Application.UseCases.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.UseCases.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.UseCases.Users.Queries.ListProjectUsers;
using SmkDoc.Application.UseCases.Users.Commands.InviteUser;
using SmkDoc.Application.UseCases.Users.Commands.UpdateUserRole;
using SmkDoc.Application.UseCases.Users.Commands.RemoveUser;
using SmkDoc.Application.UseCases.Users.Commands.SetUserStatus;

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
        services.AddScoped<CreateTemplateUseCase>();
        services.AddScoped<UpdateTemplateDetailsUseCase>();
        services.AddScoped<ActivateTemplateVersionUseCase>();
        services.AddScoped<DeactivateTemplateUseCase>();
        services.AddScoped<RollbackTemplateVersionUseCase>();
        services.AddScoped<GetTemplateByIdUseCase>();
        services.AddScoped<ListTemplatesUseCase>();
        services.AddScoped<ListTemplateVersionsUseCase>();
        services.AddScoped<DownloadTemplateUseCase>();
        services.AddScoped<ScanTemplatePlaceholdersUseCase>();
        services.AddScoped<IHtmlStudioUseCase, HtmlStudioUseCase>();
        services.AddScoped<IHtmlPersistenceUseCase, HtmlPersistenceUseCase>();
        services.AddScoped<TemplateValidateUseCase>();
        services.AddScoped<ValidateTemplatePayloadUseCase>();
        services.AddScoped<UseCases.Schemas.ValidateStandaloneSchemaUseCase>();
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
        services.AddScoped<ListProjectUsersUseCase>();
        services.AddScoped<InviteUserUseCase>();
        services.AddScoped<UpdateUserRoleUseCase>();
        services.AddScoped<RemoveUserUseCase>();
        services.AddScoped<SetUserStatusUseCase>();

        // Register all FluentValidation validators in this assembly
        services.AddValidatorsFromAssemblyContaining<GenerateDocumentCommandValidator>();

        return services;
    }
}

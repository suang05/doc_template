using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.GrantBreakGlassAccess;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.RevokeBreakGlassAccess;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.ListBreakGlassGrants;
using SmkDoc.Application.Modules.TenantManagement.Fonts.Commands.RegisterPlatformFont;
using SmkDoc.Application.Modules.TenantManagement.Fonts.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Fonts.Queries.ListPlatformFonts;
using SmkDoc.Application.Modules.TenantManagement.Projects.Commands.ActivateProject;
using SmkDoc.Application.Modules.TenantManagement.Projects.Commands.DeactivateProject;
using SmkDoc.Application.Modules.TenantManagement.Projects.Commands.UpdateProjectEgressCidrs;
using SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;
using SmkDoc.Application.Modules.TenantManagement.Quotas.Commands.UpsertProjectQuota;
using SmkDoc.Application.Modules.TenantManagement.Quotas.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Quotas.Queries.GetProjectQuota;

namespace SmkDoc.Application.Modules.TenantManagement;

public static class TenantManagementModuleExtensions
{
    public static IServiceCollection AddTenantManagementModule(this IServiceCollection services)
    {
        // Control Plane Projects
        services.AddUseCase<DeactivateProjectCommand, ProjectControlPlaneResultDto, DeactivateProjectUseCase>();
        services.AddUseCase<ActivateProjectCommand, ProjectControlPlaneResultDto, ActivateProjectUseCase>();
        services.AddUseCase<UpdateProjectEgressCidrsCommand, ProjectControlPlaneResultDto, UpdateProjectEgressCidrsUseCase>();
        services.AddUseCase<ListControlPlaneProjectsQuery, ProjectPagedResultDto, ListControlPlaneProjectsUseCase>();

        // Quotas
        services.AddUseCase<GetProjectQuotaQuery, ProjectQuotaResultDto, GetProjectQuotaUseCase>();
        services.AddUseCase<UpsertProjectQuotaCommand, ProjectQuotaResultDto, UpsertProjectQuotaUseCase>();

        // Platform Font Catalog
        services.AddUseCase<RegisterPlatformFontCommand, PlatformFontResultDto, RegisterPlatformFontUseCase>();
        services.AddUseCase<ListPlatformFontsQuery, IReadOnlyList<PlatformFontResultDto>, ListPlatformFontsUseCase>();

        // Zero Standing Access Break-Glass Grants
        services.AddUseCase<GrantBreakGlassAccessCommand, BreakGlassGrantResultDto, GrantBreakGlassAccessUseCase>();
        services.AddUseCase<RevokeBreakGlassAccessCommand, RevokeBreakGlassAccessUseCase>();
        services.AddUseCase<ListBreakGlassGrantsQuery, IReadOnlyList<BreakGlassGrantResultDto>, ListBreakGlassGrantsUseCase>();
        services.AddUseCase<GetBreakGlassGrantByIdQuery, BreakGlassGrantResultDto, GetBreakGlassGrantByIdUseCase>();

        return services;
    }
}

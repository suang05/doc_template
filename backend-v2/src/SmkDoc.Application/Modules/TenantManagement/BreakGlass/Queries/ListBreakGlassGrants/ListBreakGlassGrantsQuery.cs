namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.ListBreakGlassGrants;

public sealed record ListBreakGlassGrantsQuery(
    Guid? SuperAdminUserId = null,
    Guid? TargetProjectId = null);

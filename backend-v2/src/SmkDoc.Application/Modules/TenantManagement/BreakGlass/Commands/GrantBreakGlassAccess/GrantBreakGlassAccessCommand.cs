namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.GrantBreakGlassAccess;

public sealed record GrantBreakGlassAccessCommand(
    Guid TargetProjectId,
    Guid SuperAdminUserId,
    Guid GrantedByUserId,
    string Reason,
    int DurationMinutes = 60);

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.RevokeBreakGlassAccess;

public sealed record RevokeBreakGlassAccessCommand(Guid GrantId, long ExpectedVersion = 1);

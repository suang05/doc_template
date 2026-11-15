namespace SmkDoc.Application.Modules.TenantManagement.Fonts.Commands.RegisterPlatformFont;

public sealed record RegisterPlatformFontCommand(
    Stream Data,
    string FileName,
    string ContentType,
    string? FamilyName = null,
    string Weight = "normal",
    string Style = "normal");

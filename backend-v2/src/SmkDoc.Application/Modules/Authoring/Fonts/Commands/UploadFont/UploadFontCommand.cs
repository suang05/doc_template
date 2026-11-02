namespace SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;

public sealed record UploadFontCommand(Stream Data, string FileName, string ContentType);

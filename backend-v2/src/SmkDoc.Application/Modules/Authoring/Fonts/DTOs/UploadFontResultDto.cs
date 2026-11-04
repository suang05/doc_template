namespace SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

/// <summary>
/// Result DTO returned after successfully uploading a font file to storage.
/// </summary>
public sealed record UploadFontResultDto(string Message, string FontName);

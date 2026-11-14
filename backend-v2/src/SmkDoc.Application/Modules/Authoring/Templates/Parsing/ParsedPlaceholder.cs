namespace SmkDoc.Application.Modules.Authoring.Templates.Parsing;

public sealed record ParsedPlaceholder(PlaceholderKind Kind, string Key, string? Extra = null);

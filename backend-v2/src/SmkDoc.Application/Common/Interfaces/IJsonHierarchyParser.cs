namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Parses a JSON string into a nested Dictionary/List hierarchy for Handlebars template binding (HTML engine).
/// </summary>
public interface IJsonHierarchyParser
{
    Dictionary<string, object> ToHierarchy(string json);
}

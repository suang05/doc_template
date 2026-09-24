namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Unified JSON data parser and normalizer shared across HTML, DOCX, and XLSX rendering engines.
/// </summary>
public interface IJsonDataParser
{
    /// <summary>
    /// Parses a JSON string into a nested Dictionary/List hierarchy suitable for Handlebars template binding.
    /// </summary>
    Dictionary<string, object> ToHierarchy(string json);

    /// <summary>
    /// Flattens a JSON string into top-level key-value replacements and dynamic table row collections
    /// suitable for OpenXML (Word) engines.
    /// </summary>
    (Dictionary<string, string> Replacements, List<List<Dictionary<string, string>>> Tables) Flatten(string json);

    /// <summary>
    /// Flattens a JSON string into top-level key-value replacements and named array collections
    /// suitable for ClosedXML (Excel) template engines.
    /// </summary>
    (Dictionary<string, string> Replacements, Dictionary<string, List<Dictionary<string, string>>> Arrays) FlattenNamed(string json);
}

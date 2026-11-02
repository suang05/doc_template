namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Flattens a JSON string into top-level key-value replacements and dynamic table row collections
/// suitable for OpenXML (Word) document template engines.
/// </summary>
public interface IJsonTableParser
{
    (Dictionary<string, string> Replacements, List<List<Dictionary<string, string>>> Tables) Flatten(string json);
}

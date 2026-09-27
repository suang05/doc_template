namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Flattens a JSON string into key-value replacements and named array collections for ClosedXML (Excel) engines.
/// </summary>
public interface IJsonNamedArrayParser
{
    (Dictionary<string, string> Replacements, Dictionary<string, List<Dictionary<string, string>>> Arrays) FlattenNamed(string json);
}

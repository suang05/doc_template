namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Unified contract for parsing and transforming JSON template data payloads
/// into nested object hierarchies (HTML Handlebars) and tabular/named arrays (Word OpenXml / Excel ClosedXML).
/// </summary>
public interface ITemplateDataParser
{
    Dictionary<string, object> ToHierarchy(string json);
    (Dictionary<string, string> Replacements, List<List<Dictionary<string, string>>> Tables) Flatten(string json);
    (Dictionary<string, string> Replacements, Dictionary<string, List<Dictionary<string, string>>> Arrays) FlattenNamed(string json);
}


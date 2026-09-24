namespace SmkDocServer.Domain.Interfaces;

/// <summary>
/// Inspects a template file and extracts its placeholder schema (variable names, table names, etc.).
/// Used by the Field Mapping UI to show users what variables are available in a template.
/// </summary>
public interface ITemplateSchemaService
{
    /// <summary>
    /// Inspects the template and returns a structured schema of all detected placeholders.
    /// For .docx: scans text runs for {{variable}} patterns.
    /// For .xlsx: scans cell values for {{variable}} patterns.
    /// Returns an anonymous object suitable for JSON serialization to the frontend.
    /// </summary>
    Task<object> InspectSchemaAsync(string fileName, Guid? projectId = null);
}

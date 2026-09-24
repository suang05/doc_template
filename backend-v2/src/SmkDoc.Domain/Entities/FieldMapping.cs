namespace SmkDoc.Domain.Entities;

public class FieldMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public string Placeholder { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? Transform { get; set; }
    public int SortOrder { get; set; }
    
    public string DataSourceType { get; set; } = "json"; // json, sql
    public string? DatasetAlias { get; set; }  // references TemplateDataset.Alias for this template
    public string? ResultPath { get; set; }    // dot-notation path into dataset result JSON
    public string? MathExpression { get; set; } // optional math expression applied after json/sql resolve

    // Navigation property
    public Template? Template { get; set; }
}

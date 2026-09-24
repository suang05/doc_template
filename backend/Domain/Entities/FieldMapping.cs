using System;

namespace SmkDocServer.Domain.Entities;

public class FieldMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TemplateId { get; set; }
    public TemplateMetadata? Template { get; set; }

    /// <summary>
    /// The tag name inside the template (e.g. "buyerName", "salePrice", "contractDate")
    /// </summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>
    /// Dot-notated JSON path to extract value from caller payload (e.g. "customer.fullName", "property.price", "meta.createdAt")
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly label in Thai or English (e.g. "ชื่อผู้ซื้อ", "ราคาขาย")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Type of placeholder — controls how mapped data is routed into DocumentProcessingData.
    /// Values: "text" | "table" | "qrcode" | "barcode" | "image"
    /// </summary>
    public string PlaceholderType { get; set; } = "text";

    /// <summary>
    /// Optional transform function: "formatThaiBaht", "formatThaiDate", "formatThaiDateTime", "formatCurrency", "formatPhone", "formatThaiId"
    /// </summary>
    public string? Transform { get; set; }

    /// <summary>
    /// Fallback default value if JSON path is missing or null
    /// </summary>
    public string? DefaultValue { get; set; }

    public bool IsRequired { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    /// <summary>Where value comes from: "json" (default) | "sql" | "expr"</summary>
    public string DataSourceType { get; set; } = "json";

    /// <summary>FK to DataConnection — only used when DataSourceType = "sql"</summary>
    public Guid? DataConnectionId { get; set; }
    public DataConnection? DataConnection { get; set; }

    /// <summary>
    /// SQL SELECT query for sql-type mappings.
    /// SourcePath is used as the column name in the result set to map to this Placeholder.
    /// </summary>
    public string? SqlQuery { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

namespace SmkDoc.Domain.ValueObjects;

public enum PlaceholderCategory
{
    Scalar = 1,   // Flat primitive: customerName, invoiceDate, totalAmount
    Tabular = 2,  // Repeating collection item: items.name, products[].price
    Object = 3,   // Nested object: customer.address.city
    System = 4    // Built-in / Helper: @index, @first, pageNumber
}

public sealed record TemplatePlaceholder(
    string Name,
    PlaceholderCategory Category,
    string? ParentCollection = null,
    string? DataType = "string",
    string? SampleValue = null
)
{
    public static TemplatePlaceholder Scalar(string name, string? dataType = "string", string? sampleValue = null) =>
        new(name, PlaceholderCategory.Scalar, null, dataType, sampleValue);

    public static TemplatePlaceholder Tabular(string name, string parentCollection, string? dataType = "string", string? sampleValue = null) =>
        new(name, PlaceholderCategory.Tabular, parentCollection, dataType, sampleValue);

    public static TemplatePlaceholder Object(string name, string? dataType = "object", string? sampleValue = null) =>
        new(name, PlaceholderCategory.Object, null, dataType, sampleValue);

    public static TemplatePlaceholder System(string name, string? sampleValue = null) =>
        new(name, PlaceholderCategory.System, null, "string", sampleValue);
}


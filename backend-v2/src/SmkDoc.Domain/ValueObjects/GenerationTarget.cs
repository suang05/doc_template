using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Domain parameter record encapsulating template targets, output payloads, and artifact keys.
/// </summary>
public sealed record GenerationTarget(
    TemplateId? TemplateId = null,
    Guid? TemplateVersionId = null,
    OutputFormat? OutputFormat = null,
    string? OutputKey = null,
    string? InputData = null,
    Sha256Hash? PayloadHashSha256 = null)
{
    public static GenerationTarget ForTemplate(
        TemplateId templateId,
        Guid templateVersionId,
        OutputFormat outputFormat,
        string? inputData = null,
        string? outputKey = null) =>
        new(templateId, templateVersionId, outputFormat, outputKey, inputData);
}

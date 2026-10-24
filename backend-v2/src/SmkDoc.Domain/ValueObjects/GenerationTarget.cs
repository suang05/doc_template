using System.Security.Cryptography;
using System.Text;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.ValueObjects;

/// <summary>
/// Domain parameter record encapsulating template targets, output payloads, and artifact keys.
/// Automatically computes deterministic SHA-256 hash when inputData is provided.
/// </summary>
public sealed record GenerationTarget
{
    public TemplateId? TemplateId { get; init; }
    public Guid? TemplateVersionId { get; init; }
    public OutputFormat? OutputFormat { get; init; }
    public string? OutputKey { get; init; }
    public string? InputData { get; init; }
    public Sha256Hash? PayloadHashSha256 { get; init; }

    public GenerationTarget(
        TemplateId? templateId = null,
        Guid? templateVersionId = null,
        OutputFormat? outputFormat = null,
        string? outputKey = null,
        string? inputData = null,
        Sha256Hash? payloadHashSha256 = null)
    {
        TemplateId = templateId;
        TemplateVersionId = templateVersionId;
        OutputFormat = outputFormat;
        OutputKey = outputKey;
        InputData = inputData;
        PayloadHashSha256 = payloadHashSha256 ?? ComputeHash(inputData);
    }

    private static Sha256Hash? ComputeHash(string? inputData)
    {
        if (string.IsNullOrWhiteSpace(inputData)) return null;
        var bytes = Encoding.UTF8.GetBytes(inputData);
        var hashBytes = SHA256.HashData(bytes);
        return new Sha256Hash(Convert.ToHexStringLower(hashBytes));
    }

    public static GenerationTarget ForTemplate(
        TemplateId templateId,
        Guid templateVersionId,
        OutputFormat outputFormat,
        string? inputData = null,
        string? outputKey = null,
        Sha256Hash? payloadHash = null) =>
        new(templateId, templateVersionId, outputFormat, outputKey, inputData, payloadHash);
}

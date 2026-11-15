using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// Single document render payload entry within an asynchronous Pipeline C document job.
/// </summary>
public sealed record CreateDocumentJobItemRequest(
    [property: JsonRequired] JsonElement Payload,
    string? DocumentRef = null);

/// <summary>
/// HTTP request contract for submitting an asynchronous single or batch Pipeline C rendering job.
/// Accepts either <see cref="Items"/> for batch jobs or <see cref="Payload"/> for single-document convenience.
/// </summary>
public sealed record CreateDocumentJobRequest(
    string Slug,
    IReadOnlyList<CreateDocumentJobItemRequest>? Items = null,
    JsonElement? Payload = null,
    string OutputFormat = "pdf",
    string? DocumentRef = null,
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Webhook callback URL validated by FluentValidation and egress SSRF validator.")]
    string? WebhookUrl = null,
    int MaxAttempts = DocumentJob.DefaultMaxAttempts)
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Webhook callback URL validated by FluentValidation and egress SSRF validator.")]
    public string? WebhookUrl { get; init; } = WebhookUrl;
}

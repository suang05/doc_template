using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.CreateDocumentJob;

public sealed record CreateDocumentJobCommand(
    string Slug,
    IReadOnlyList<DocumentJobItemInput> Items,
    string OutputFormat = "pdf",
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Webhook callback URI accepted as string in wire contract before validation.")]
    string? WebhookUrl = null,
    string? IdempotencyKey = null,
    string? RequestFingerprint = null,
    int MaxAttempts = DocumentJob.DefaultMaxAttempts) : IIdempotentCommand
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Webhook callback URI accepted as string in wire contract before validation.")]
    public string? WebhookUrl { get; init; } = WebhookUrl;

    [JsonIgnore]
    public string IdempotencyScope => $"document-jobs:{Slug}";
}

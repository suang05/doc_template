using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

/// <summary>
/// Canonical persisted execution lifecycle state for async Pipeline C document jobs and items.
/// </summary>
public sealed class DocumentJobStatus : Enumeration
{
    public static readonly DocumentJobStatus Queued = new(1, "Queued");
    public static readonly DocumentJobStatus Running = new(2, "Running");
    public static readonly DocumentJobStatus Succeeded = new(3, "Succeeded");
    public static readonly DocumentJobStatus Failed = new(4, "Failed");
    public static readonly DocumentJobStatus Canceled = new(5, "Canceled");

    private DocumentJobStatus(int id, string name) : base(id, name) { }

    public static DocumentJobStatus FromName(string name) => FromDisplayName<DocumentJobStatus>(name);

    public bool IsTerminal =>
        this == Succeeded || this == Failed || this == Canceled;

    public static implicit operator string(DocumentJobStatus status) => status.Name;
}

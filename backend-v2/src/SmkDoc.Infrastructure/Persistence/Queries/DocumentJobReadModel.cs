using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class DocumentJobReadModel(AppDbContext context) : IDocumentJobReadModel
{
    public async Task<DocumentJobResultDto?> FindByIdAsync(DocumentJobId jobId, CancellationToken ct)
    {
        return await context.DocumentJobs
            .AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new DocumentJobResultDto(
                j.Id.Value,
                j.TemplateSlug,
                j.TemplateId.Value,
                j.TemplateVersionId,
                j.Status.Name,
                j.OutputFormat.Extension,
                j.Attempt,
                j.MaxAttempts,
                j.CancelRequested,
                j.Items.Count,
                j.Items.Count(i => i.Status == DocumentJobStatus.Succeeded),
                j.Items.Count(i => i.Status == DocumentJobStatus.Failed),
                j.ResultGenerationLogId,
                j.Status == DocumentJobStatus.Succeeded && !string.IsNullOrEmpty(j.ResultStorageKey)
                    ? $"/api/v1/document-jobs/{j.Id.Value}/result"
                    : null,
                j.LastErrorCode,
                j.LastErrorMessage,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.Version))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DocumentJobListResult> ListPagedAsync(string? status, string? cursor, int limit, CancellationToken ct)
    {
        var query = context.DocumentJobs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = DocumentJobStatus.FromName(status.Trim());
            query = query.Where(j => j.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var (cursorCreatedAt, cursorId) = DecodeCursor(cursor);
            var typedCursorId = new DocumentJobId(cursorId);
            query = query.Where(j =>
                j.CreatedAt < cursorCreatedAt ||
                (j.CreatedAt == cursorCreatedAt && j.Id.CompareTo(typedCursorId) < 0));
        }

        int safeLimit = limit < 1 ? 20 : Math.Min(limit, 100);

        var rows = await query
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Take(safeLimit + 1)
            .Select(j => new DocumentJobResultDto(
                j.Id.Value,
                j.TemplateSlug,
                j.TemplateId.Value,
                j.TemplateVersionId,
                j.Status.Name,
                j.OutputFormat.Extension,
                j.Attempt,
                j.MaxAttempts,
                j.CancelRequested,
                j.Items.Count,
                j.Items.Count(i => i.Status == DocumentJobStatus.Succeeded),
                j.Items.Count(i => i.Status == DocumentJobStatus.Failed),
                j.ResultGenerationLogId,
                j.Status == DocumentJobStatus.Succeeded && !string.IsNullOrEmpty(j.ResultStorageKey)
                    ? $"/api/v1/document-jobs/{j.Id.Value}/result"
                    : null,
                j.LastErrorCode,
                j.LastErrorMessage,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.Version))
            .ToListAsync(ct);

        bool hasMore = rows.Count > safeLimit;
        var items = hasMore ? rows.Take(safeLimit).ToList() : rows;
        string? nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].CreatedAt, items[^1].JobId)
            : null;

        return new DocumentJobListResult(items, nextCursor, hasMore);
    }

    public async Task<IReadOnlyList<DocumentJobItemDto>> ListItemsAsync(DocumentJobId jobId, CancellationToken ct)
    {
        return await context.DocumentJobItems
            .AsNoTracking()
            .Where(i => i.DocumentJobId == jobId)
            .OrderBy(i => i.ItemIndex)
            .Select(i => new DocumentJobItemDto(
                i.Id,
                i.ItemIndex,
                i.DocumentRef,
                i.Status.Name,
                i.GenerationLogId,
                i.OutputStorageKey,
                i.ErrorCode,
                i.ErrorMessage,
                i.CreatedAt,
                i.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<DocumentJobArtifactDescriptor?> FindArtifactAsync(DocumentJobId jobId, CancellationToken ct)
    {
        var job = await context.DocumentJobs
            .AsNoTracking()
            .Where(j => j.Id == jobId && j.Status == DocumentJobStatus.Succeeded && !string.IsNullOrEmpty(j.ResultStorageKey))
            .Select(j => new
            {
                j.Id,
                j.TemplateSlug,
                j.OutputFormat,
                j.ResultStorageKey
            })
            .FirstOrDefaultAsync(ct);

        if (job is null || string.IsNullOrWhiteSpace(job.ResultStorageKey))
        {
            return null;
        }

        bool isZip = job.ResultStorageKey.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        string contentType = isZip ? "application/zip" : job.OutputFormat.MimeType;
        string ext = isZip ? "zip" : job.OutputFormat.Extension;
        string fileName = $"{job.TemplateSlug}-{job.Id.Value:N}.{ext}";

        return new DocumentJobArtifactDescriptor(
            job.Id.Value,
            job.ResultStorageKey,
            contentType,
            fileName);
    }

    private static string EncodeCursor(DateTimeOffset createdAt, Guid jobId)
    {
        var raw = string.Create(CultureInfo.InvariantCulture, $"{createdAt.ToUniversalTime():O}|{jobId:D}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTimeOffset CreatedAt, Guid JobId) DecodeCursor(string cursor)
    {
        try
        {
            byte[] bytes = Base64Url.DecodeFromChars(cursor.Trim());
            string raw = Encoding.UTF8.GetString(bytes);
            int separatorIndex = raw.IndexOf('|', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                throw new FormatException("Cursor separator missing.");
            }

            var createdAt = DateTimeOffset.ParseExact(
                raw[..separatorIndex],
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var jobId = Guid.Parse(raw[(separatorIndex + 1)..]);
            return (createdAt, jobId);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new ValidationException("cursor", "Invalid pagination cursor.");
        }
    }
}

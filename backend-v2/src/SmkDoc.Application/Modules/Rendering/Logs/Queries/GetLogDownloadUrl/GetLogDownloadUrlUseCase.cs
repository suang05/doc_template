using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

public sealed class GetLogDownloadUrlUseCase(
    IRepository<GenerationLog> logRepo,
    IStorageService storageService) : IUseCase<GetLogDownloadUrlQuery, string>
{
    private readonly IRepository<GenerationLog> _logRepo = logRepo;
    private readonly IStorageService _storageService = storageService;

    public async Task<string> ExecuteAsync(GetLogDownloadUrlQuery query, CancellationToken ct = default)
    {
        var log = await _logRepo.GetByIdAsync(query.LogId, ct)
            ?? throw new NotFoundException($"Generation log '{query.LogId}' not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new InvalidOperationException("No output file is associated with this log entry.");

        return await _storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, log.OutputKey, TimeSpan.FromHours(1), ct);
    }
}

using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

public sealed class GetLogDownloadUrlUseCase(
    IGenerationLogRepository logRepo,
    IStorageService storageService) : IUseCase<GetLogDownloadUrlQuery, DownloadUrlResultDto>
{
    public async Task<DownloadUrlResultDto> ExecuteAsync(GetLogDownloadUrlQuery query, CancellationToken ct = default)
    {
        var log = await logRepo.GetByIdAsync(query.LogId, ct)
            ?? throw new NotFoundException($"Generation log '{query.LogId}' not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new NotFoundException("No output file is associated with this log entry.");

        var url = await storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, log.OutputKey, TimeSpan.FromHours(1), ct);
        return new DownloadUrlResultDto(url, 3600);
    }
}

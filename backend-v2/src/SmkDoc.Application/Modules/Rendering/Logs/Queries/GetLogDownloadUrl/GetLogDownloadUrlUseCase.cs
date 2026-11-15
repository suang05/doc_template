using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

public sealed class GetLogDownloadUrlUseCase(
    IGenerationLogRepository logRepo) : IUseCase<GetLogDownloadUrlQuery, DownloadUrlResultDto>
{
    public async Task<DownloadUrlResultDto> ExecuteAsync(GetLogDownloadUrlQuery query, CancellationToken ct)
    {
        var log = await logRepo.GetByIdAsync(query.LogId, ct)
            ?? throw new NotFoundException($"Generation log '{query.LogId}' not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new NotFoundException("No output file is associated with this log entry.");

        return new DownloadUrlResultDto($"/api/v1/generation-logs/{log.Id:D}/artifact/file", 3600);
    }
}

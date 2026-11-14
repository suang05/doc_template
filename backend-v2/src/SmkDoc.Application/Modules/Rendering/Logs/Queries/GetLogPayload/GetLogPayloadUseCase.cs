using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogPayload;

public sealed class GetLogPayloadUseCase(
    IGenerationLogRepository logRepo,
    IStorageService storageService) : IUseCase<GetLogPayloadQuery, LogPayloadResultDto>
{
    private const string OffloadedPrefix = "[OFFLOADED:";

    public async Task<LogPayloadResultDto> ExecuteAsync(GetLogPayloadQuery query, CancellationToken ct)
    {
        var log = await logRepo.GetByIdAsync(query.LogId, ct)
            ?? throw new NotFoundException($"Generation log '{query.LogId}' not found.");

        if (string.IsNullOrWhiteSpace(log.InputData))
        {
            return new LogPayloadResultDto(log.Id, null, false, null);
        }

        if (log.InputData.StartsWith(OffloadedPrefix, StringComparison.Ordinal) && log.InputData.EndsWith(']'))
        {
            var objectKey = log.InputData[OffloadedPrefix.Length..^1];
            await using var stream = await storageService.DownloadAsync(StorageBuckets.AuditPayloads, objectKey, ct);
            using var reader = new StreamReader(stream);
            var payloadJson = await reader.ReadToEndAsync(ct);

            return new LogPayloadResultDto(log.Id, payloadJson, true, objectKey);
        }

        return new LogPayloadResultDto(log.Id, log.InputData, false, null);
    }
}

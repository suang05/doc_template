using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

public static class GenerationLogTestFactory
{
    public static GenerationLog Create(
        Guid? id = null,
        Guid? templateId = null,
        Guid? templateVersionId = null,
        Guid? apiKeyId = null,
        string? callerApp = "TestApp",
        string? triggerSource = "api",
        string? inputData = "{}",
        string? outputKey = "outputs/sample.pdf",
        OutputFormat? outputFormat = null,
        long? fileSizeBytes = 1024,
        int? pageCount = 1,
        Sha256Hash? payloadHashSha256 = null,
        int durationMs = 100,
        GenerationStatus? status = null,
        string? errorMsg = null,
        DateTimeOffset? now = null)
    {
        return new GenerationLog(
            id ?? Guid.NewGuid(),
            templateId,
            templateVersionId,
            apiKeyId,
            callerApp,
            triggerSource,
            inputData,
            outputKey,
            outputFormat ?? OutputFormat.Pdf,
            fileSizeBytes,
            pageCount,
            payloadHashSha256,
            durationMs,
            status ?? GenerationStatus.Success,
            errorMsg,
            now ?? TestConstants.BaselineTime);
    }
}

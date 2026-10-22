using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class GenerationLogBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid? _templateId = Guid.NewGuid();
    private Guid? _templateVersionId = Guid.NewGuid();
    private Guid? _apiKeyId = null;
    private string? _callerApp = "BillingService";
    private string? _triggerSource = "api";
    private string? _inputData = "{}";
    private string? _outputKey = "outputs/sample.pdf";
    private OutputFormat _outputFormat = OutputFormat.Pdf;
    private long? _fileSizeBytes = 1024;
    private int? _pageCount = 1;
    private Sha256Hash? _payloadHashSha256 = null;
    private int _durationMs = 150;
    private GenerationStatus _status = GenerationStatus.Success;
    private string? _errorMsg = null;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static GenerationLogBuilder ALog() => new();

    public static GenerationLog Default() => ALog().Build();

    public static GenerationLog Success(
        Guid? templateId = null,
        Guid? templateVersionId = null,
        string outputKey = "outputs/sample.pdf",
        int durationMs = 150,
        Guid? id = null) =>
        new GenerationLogBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTemplateId(templateId ?? Guid.NewGuid())
            .WithTemplateVersionId(templateVersionId ?? Guid.NewGuid())
            .WithOutputKey(outputKey)
            .WithDurationMs(durationMs)
            .Build();

    public static GenerationLog Failed(
        string errorMessage,
        Guid? templateId = null,
        Guid? templateVersionId = null,
        Guid? id = null) =>
        new GenerationLogBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTemplateId(templateId ?? Guid.NewGuid())
            .WithTemplateVersionId(templateVersionId ?? Guid.NewGuid())
            .AsFailed(errorMessage)
            .Build();


    public GenerationLogBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public GenerationLogBuilder WithTemplateId(Guid? templateId)
    {
        _templateId = templateId;
        return this;
    }

    public GenerationLogBuilder WithTemplateVersionId(Guid? templateVersionId)
    {
        _templateVersionId = templateVersionId;
        return this;
    }

    public GenerationLogBuilder WithApiKeyId(Guid? apiKeyId)
    {
        _apiKeyId = apiKeyId;
        return this;
    }

    public GenerationLogBuilder WithCallerApp(string? callerApp)
    {
        _callerApp = callerApp;
        return this;
    }

    public GenerationLogBuilder WithTriggerSource(string? triggerSource)
    {
        _triggerSource = triggerSource;
        return this;
    }

    public GenerationLogBuilder WithInputData(string? inputData)
    {
        _inputData = inputData;
        return this;
    }

    public GenerationLogBuilder WithOutputKey(string? outputKey)
    {
        _outputKey = outputKey;
        return this;
    }

    public GenerationLogBuilder WithOutputFormat(OutputFormat format)
    {
        _outputFormat = format;
        return this;
    }

    public GenerationLogBuilder WithFileSizeBytes(long? bytes)
    {
        _fileSizeBytes = bytes;
        return this;
    }

    public GenerationLogBuilder WithPageCount(int? pages)
    {
        _pageCount = pages;
        return this;
    }

    public GenerationLogBuilder WithPayloadHashSha256(Sha256Hash? hash)
    {
        _payloadHashSha256 = hash;
        return this;
    }

    public GenerationLogBuilder WithDurationMs(int ms)
    {
        _durationMs = ms;
        return this;
    }

    public GenerationLogBuilder WithStatus(GenerationStatus status)
    {
        _status = status;
        return this;
    }

    public GenerationLogBuilder AsFailed(string errorMsg)
    {
        _status = GenerationStatus.Failed;
        _errorMsg = errorMsg;
        return this;
    }

    public GenerationLogBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public GenerationLog Build()
    {
        return new GenerationLog(
            _id,
            _templateId,
            _templateVersionId,
            _apiKeyId,
            _callerApp,
            _triggerSource,
            _inputData,
            _outputKey,
            _outputFormat,
            _fileSizeBytes,
            _pageCount,
            _payloadHashSha256,
            _durationMs,
            _status,
            _errorMsg,
            _now);
    }

    public static implicit operator GenerationLog(GenerationLogBuilder builder) => builder.Build();
}

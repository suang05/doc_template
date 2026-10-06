using SmkDoc.Domain.Entities;

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
}

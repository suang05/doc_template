using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class GenerationLogTests
{
    private static readonly DateTimeOffset Now = TestConstants.BaselineTime;

    private static Action CreateLog(
        string? callerApp = null,
        string triggerSource = "api",
        string? outputKey = null,
        long? fileSizeBytes = null,
        int? pageCount = null,
        int durationMs = 10) =>
        () => GenerationLog.Create(null, null, null, callerApp, triggerSource, null, outputKey, null, fileSizeBytes, pageCount, null, durationMs, GenerationStatus.Success, null, Now);

    [Fact]
    public void CreateSuccess_WithValidParameters_InitializesCorrectly()
    {
        var generationId = Guid.NewGuid();
        var templateId = TemplateId.New();
        var templateVersionId = Guid.NewGuid();
        var apiKeyId = ApiKeyId.New();

        var log = GenerationLog.CreateSuccess(
            generationId,
            templateId,
            templateVersionId,
            apiKeyId,
            "crm-service",
            "api",
            "{\"invoiceNo\":\"INV-001\"}",
            "outputs/invoice.pdf",
            OutputFormat.Pdf,
            20480,
            120,
            Now);

        log.Id.Should().Be(generationId);
        log.TemplateId.Should().Be(templateId);
        log.TemplateVersionId.Should().Be(templateVersionId);
        log.ApiKeyId.Should().Be(apiKeyId);
        log.CallerApp.Should().Be("crm-service");
        log.TriggerSource.Should().Be("api");
        log.InputData.Should().Be("{\"invoiceNo\":\"INV-001\"}");
        log.OutputKey.Should().Be("outputs/invoice.pdf");
        log.OutputFormat.Should().Be(OutputFormat.Pdf);
        log.FileSizeBytes.Should().Be(20480);
        log.DurationMs.Should().Be(120);
        log.Status.Should().Be(GenerationStatus.Success);
        log.ErrorMsg.Should().BeNull();
        log.CreatedAt.Should().Be(Now);
        log.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateValidationFailure_SetsValidationFailedStatusAndErrorMessage()
    {
        var templateId = TemplateId.New();
        var templateVersionId = Guid.NewGuid();

        var log = GenerationLog.CreateValidationFailure(
            templateId,
            templateVersionId,
            null,
            "portal",
            "ui",
            "{}",
            OutputFormat.Pdf,
            15,
            "Payload missing required field 'contractId'",
            Now);

        log.Status.Should().Be(GenerationStatus.ValidationFailed);
        log.ErrorMsg.Should().Be("Payload missing required field 'contractId'");
        log.FileSizeBytes.Should().Be(0);
        log.OutputKey.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_SetsFailedStatusAndErrorMessage()
    {
        var templateId = TemplateId.New();
        var templateVersionId = Guid.NewGuid();

        var log = GenerationLog.CreateFailure(
            templateId,
            templateVersionId,
            null,
            "portal",
            "api",
            "{}",
            OutputFormat.Pdf,
            3000,
            "Gotenberg service timeout",
            Now);

        log.Status.Should().Be(GenerationStatus.Failed);
        log.ErrorMsg.Should().Be("Gotenberg service timeout");
    }

    [Theory]
    [InlineData("DurationMs", -1, null, null)]
    [InlineData("FileSizeBytes", 10, -50L, null)]
    [InlineData("PageCount", 10, null, -1)]
    public void Create_WithNegativeNumeric_ThrowsDomainValidationException(string fieldName, int durationMs, long? fileSizeBytes, int? pageCount)
    {
        var act = CreateLog(durationMs: durationMs, fileSizeBytes: fileSizeBytes, pageCount: pageCount);
        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*{fieldName} cannot be negative*");
    }

    [Theory]
    [InlineData("CallerApp")]
    [InlineData("TriggerSource")]
    [InlineData("OutputKey")]
    public void Create_WithFieldExceedingMaxLength_ThrowsDomainValidationException(string field)
    {
        Action act = field switch
        {
            "CallerApp" => CreateLog(callerApp: new string('A', GenerationLog.MaxCallerAppLength + 1)),
            "TriggerSource" => CreateLog(triggerSource: new string('S', GenerationLog.MaxTriggerSourceLength + 1)),
            "OutputKey" => CreateLog(outputKey: new string('K', GenerationLog.MaxOutputKeyLength + 1)),
            _ => throw new ArgumentException($"Unknown field {field}")
        };

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*{field} must not exceed * characters*");
    }

    [Fact]
    public void Create_WithSmartEnumStatus_InitializesSuccessfully()
    {
        var log = GenerationLog.Create(null, null, null, null, "api", null, null, null, null, null, null, 10, GenerationStatus.Timeout, null, Now);

        log.Status.Should().Be(GenerationStatus.Timeout);
    }
}

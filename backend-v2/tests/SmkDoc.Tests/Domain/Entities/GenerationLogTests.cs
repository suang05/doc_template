using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class GenerationLogTests
{
    [Fact]
    public void CreateSuccess_WithValidParameters_InitializesCorrectly()
    {
        var generationId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero);

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
            now);

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
        log.CreatedAt.Should().Be(now);
        log.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateValidationFailure_SetsValidationFailedStatusAndErrorMessage()
    {
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var now = TestConstants.BaselineTime;

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
            now);

        log.Status.Should().Be(GenerationStatus.ValidationFailed);
        log.ErrorMsg.Should().Be("Payload missing required field 'contractId'");
        log.FileSizeBytes.Should().Be(0);
        log.OutputKey.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_SetsFailedStatusAndErrorMessage()
    {
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var now = TestConstants.BaselineTime;

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
            now);

        log.Status.Should().Be(GenerationStatus.Failed);
        log.ErrorMsg.Should().Be("Gotenberg service timeout");
    }

    [Fact]
    public void Create_WithNegativeDurationMs_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var act = () => GenerationLog.Create(null, null, null, null, null, null, null, null, null, null, null, -1, GenerationStatus.Success, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DurationMs cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativeFileSizeBytes_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var act = () => GenerationLog.Create(null, null, null, null, null, null, null, null, -50, null, null, 10, GenerationStatus.Success, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FileSizeBytes cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativePageCount_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var act = () => GenerationLog.Create(null, null, null, null, null, null, null, null, 100, -1, null, 10, GenerationStatus.Success, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*PageCount cannot be negative*");
    }

    [Fact]
    public void Create_WithCallerAppExceedingMaxLength_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var longApp = new string('A', GenerationLog.MaxCallerAppLength + 1);
        var act = () => GenerationLog.Create(null, null, null, longApp, null, null, null, null, null, null, null, 10, GenerationStatus.Success, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*CallerApp must not exceed {GenerationLog.MaxCallerAppLength} characters*");
    }

    [Fact]
    public void Create_WithTriggerSourceExceedingMaxLength_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var longSource = new string('S', GenerationLog.MaxTriggerSourceLength + 1);
        var act = () => GenerationLog.Create(null, null, null, null, longSource, null, null, null, null, null, null, 10, GenerationStatus.Success, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*TriggerSource must not exceed {GenerationLog.MaxTriggerSourceLength} characters*");
    }

    [Fact]
    public void Create_WithOutputKeyExceedingMaxLength_ThrowsDomainValidationException()
    {
        var now = TestConstants.BaselineTime;
        var longKey = new string('K', GenerationLog.MaxOutputKeyLength + 1);
        var act = () => GenerationLog.Create(null, null, null, null, null, null, longKey, null, null, null, null, 10, GenerationStatus.Success, null, now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*OutputKey must not exceed {GenerationLog.MaxOutputKeyLength} characters*");
    }

    [Fact]
    public void Create_WithSmartEnumStatus_InitializesSuccessfully()
    {
        var now = TestConstants.BaselineTime;
        var log = GenerationLog.Create(null, null, null, null, null, null, null, null, null, null, null, 10, GenerationStatus.Timeout, null, now);

        log.Status.Should().Be(GenerationStatus.Timeout);
    }
}

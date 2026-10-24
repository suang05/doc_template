using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class GenerationLogTests
{
    private static readonly DateTimeOffset Now = TestConstants.BaselineTime;
    private static readonly ProjectId TestProjectId = ProjectId.New();

    private static Action CreateLog(
        string? callerApp = null,
        string triggerSource = "api",
        string? outputKey = null,
        long? fileSizeBytes = null,
        int? pageCount = null,
        int durationMs = 10,
        ProjectId? projectId = null) =>
        () => GenerationLog.Create(
            projectId ?? TestProjectId,
            new GenerationTarget(null, null, null, outputKey, null, null),
            new GenerationContext(callerApp, triggerSource, null),
            new GenerationMetrics(durationMs, fileSizeBytes, pageCount),
            GenerationStatus.Success,
            Now);

    [Fact]
    public void CreateSuccess_WithValidParameters_InitializesCorrectly()
    {
        var generationId = Guid.NewGuid();
        var templateId = TemplateId.New();
        var templateVersionId = Guid.NewGuid();
        var apiKeyId = ApiKeyId.New();

        var target = new GenerationTarget(
            templateId,
            templateVersionId,
            OutputFormat.Pdf,
            "outputs/invoice.pdf",
            "{\"invoiceNo\":\"INV-001\"}");
        var context = new GenerationContext("crm-service", "api", apiKeyId);
        var metrics = GenerationMetrics.Of(120, 20480);

        var log = GenerationLog.CreateSuccess(
            generationId,
            TestProjectId,
            target,
            context,
            metrics,
            Now);

        log.Id.Should().Be(generationId);
        log.ProjectId.Should().Be(TestProjectId);
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
        log.PayloadHashSha256.Should().NotBeNull();
    }

    [Fact]
    public void CreateSuccess_WithInputData_AutoComputesSha256PayloadHash()
    {
        var inputJson = "{\"invoiceNo\":\"INV-001\"}";
        var expectedHashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(inputJson));
        var expectedHex = Convert.ToHexStringLower(expectedHashBytes);

        var target = new GenerationTarget(
            TemplateId.New(),
            Guid.NewGuid(),
            OutputFormat.Pdf,
            "outputs/invoice.pdf",
            inputJson);
        var context = new GenerationContext("crm-service", "api", ApiKeyId.New());
        var metrics = GenerationMetrics.Of(50, 1024);

        var log = GenerationLog.CreateSuccess(
            Guid.NewGuid(),
            TestProjectId,
            target,
            context,
            metrics,
            Now);

        log.PayloadHashSha256.Should().NotBeNull();
        log.PayloadHashSha256!.Value.Should().Be(expectedHex);
    }

    [Fact]
    public void CreateValidationFailure_SetsValidationFailedStatusAndErrorMessage()
    {
        var target = GenerationTarget.ForTemplate(TemplateId.New(), Guid.NewGuid(), OutputFormat.Pdf, "{}");
        var context = new GenerationContext("portal", "ui", null);

        var log = GenerationLog.CreateValidationFailure(
            TestProjectId,
            target,
            context,
            15,
            "Payload missing required field 'contractId'",
            Now);

        log.ProjectId.Should().Be(TestProjectId);
        log.Status.Should().Be(GenerationStatus.ValidationFailed);
        log.ErrorMsg.Should().Be("Payload missing required field 'contractId'");
        log.FileSizeBytes.Should().Be(0);
        log.OutputKey.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_SetsFailedStatusAndErrorMessage()
    {
        var target = GenerationTarget.ForTemplate(TemplateId.New(), Guid.NewGuid(), OutputFormat.Pdf, "{}");
        var context = new GenerationContext("portal", "api", null);

        var log = GenerationLog.CreateFailure(
            TestProjectId,
            target,
            context,
            3000,
            "Gotenberg service timeout",
            Now);

        log.ProjectId.Should().Be(TestProjectId);
        log.Status.Should().Be(GenerationStatus.Failed);
        log.ErrorMsg.Should().Be("Gotenberg service timeout");
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var target = new GenerationTarget();
        var context = new GenerationContext("app", "api");
        var metrics = GenerationMetrics.ForDuration(10, 0);

        var act = () => GenerationLog.Create(
            ProjectId.Empty,
            target,
            context,
            metrics,
            GenerationStatus.Success,
            Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
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
        var target = new GenerationTarget();
        var context = new GenerationContext(null, "api");
        var metrics = GenerationMetrics.ForDuration(10, 0);

        var log = GenerationLog.Create(
            TestProjectId,
            target,
            context,
            metrics,
            GenerationStatus.Timeout,
            Now);

        log.Status.Should().Be(GenerationStatus.Timeout);
    }

    [Fact]
    public void Create_WithGroupedParameterRecords_InitializesCorrectly()
    {
        var target = new GenerationTarget(
            TemplateId.New(),
            Guid.NewGuid(),
            OutputFormat.Pdf,
            "outputs/doc.pdf",
            "{\"key\":\"val\"}");

        var context = new GenerationContext("billing-app", "cron", ApiKeyId.New());
        var metrics = GenerationMetrics.Of(250, 40960, 2);

        var log = GenerationLog.Create(
            TestProjectId,
            target,
            context,
            metrics,
            GenerationStatus.Success,
            Now);

        log.ProjectId.Should().Be(TestProjectId);
        log.TemplateId.Should().Be(target.TemplateId);
        log.TemplateVersionId.Should().Be(target.TemplateVersionId);
        log.OutputFormat.Should().Be(OutputFormat.Pdf);
        log.OutputKey.Should().Be("outputs/doc.pdf");
        log.InputData.Should().Be("{\"key\":\"val\"}");
        log.CallerApp.Should().Be("billing-app");
        log.TriggerSource.Should().Be("cron");
        log.ApiKeyId.Should().Be(context.ApiKeyId);
        log.DurationMs.Should().Be(250);
        log.FileSizeBytes.Should().Be(40960);
        log.PageCount.Should().Be(2);
        log.Status.Should().Be(GenerationStatus.Success);
    }

    [Fact]
    public void CreateSuccess_WithGroupedParameterRecords_InitializesAsSuccess()
    {
        var genId = Guid.NewGuid();
        var target = GenerationTarget.ForTemplate(TemplateId.New(), Guid.NewGuid(), OutputFormat.Docx, "{}", "out.docx");
        var context = new GenerationContext("app", "api");
        var metrics = GenerationMetrics.Of(80, 1024);

        var log = GenerationLog.CreateSuccess(genId, TestProjectId, target, context, metrics, Now);

        log.Id.Should().Be(genId);
        log.Status.Should().Be(GenerationStatus.Success);
        log.ErrorMsg.Should().BeNull();
        log.DurationMs.Should().Be(80);
    }

    [Fact]
    public void CreateValidationFailure_WithGroupedParameterRecords_InitializesAsValidationFailed()
    {
        var target = GenerationTarget.ForTemplate(TemplateId.New(), Guid.NewGuid(), OutputFormat.Pdf, "{ invalid }");
        var context = new GenerationContext("app", "api");

        var log = GenerationLog.CreateValidationFailure(
            TestProjectId,
            target,
            context,
            15,
            "Schema invalid",
            Now);

        log.Status.Should().Be(GenerationStatus.ValidationFailed);
        log.ErrorMsg.Should().Be("Schema invalid");
        log.DurationMs.Should().Be(15);
    }
}

using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditProcessorTests
{
    private readonly IGenerationLogQueue _logQueue = Substitute.For<IGenerationLogQueue>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly TimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public DocumentAuditProcessorTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(ProjectId.New());
    }

    private DocumentAuditProcessor CreateSut(IStorageService? storage = null) => new(
        _logQueue, _projectContext, _timeProvider, storage, null);

    [Fact]
    public async Task LogValidationFailureAsync_ShouldAddValidationFailedLogWithoutPrematureCommit()
    {
        // Arrange
        _projectContext.CallerIdentity.Returns("crm-app");

        // Act
        await CreateSut().LogValidationFailureAsync(
            Guid.NewGuid(), Guid.NewGuid(), "{}", OutputFormat.Pdf, 50, "Schema violation");

        // Assert
        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l => l.Status == GenerationStatus.ValidationFailed && l.CallerApp == "crm-app"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogFailureAsync_ShouldAddFailedLogWithErrorMessage()
    {
        // Arrange
        _projectContext.CallerIdentity.Returns("erp-worker");

        // Act
        await CreateSut().LogFailureAsync(
            Guid.NewGuid(), Guid.NewGuid(), "{\"doc\":1}", OutputFormat.Pdf, 180, "Gotenberg engine timeout");

        // Assert
        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l =>
                l.Status == GenerationStatus.Failed &&
                l.CallerApp == "erp-worker" &&
                l.ErrorMsg == "Gotenberg engine timeout"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_ShouldAddSuccessLogWithoutPrematureCommit()
    {
        // Arrange
        _projectContext.CallerIdentity.Returns("pos-app");
        var genId = Guid.NewGuid();

        // Act
        await CreateSut().LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), "{}", "outputs/receipt.pdf", OutputFormat.Pdf, 1024, 150);

        // Assert
        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l => l.Id == genId && l.Status == GenerationStatus.Success && l.OutputKey == "outputs/receipt.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_WithSmallPayload_StoresPayloadInlineAndComputesSha256()
    {
        // Arrange
        _projectContext.CallerIdentity.Returns("web-portal");
        var smallJson = "{\"customerId\": 42, \"name\": \"Acme Corp\"}";
        var genId = Guid.NewGuid();

        // Act
        await CreateSut(_storageService).LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), smallJson, "outputs/invoice.pdf", OutputFormat.Pdf, 2048, 80);

        // Assert
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l =>
                l.Id == genId &&
                l.InputData == smallJson &&
                l.PayloadHashSha256 != null &&
                l.PayloadHashSha256.Value.Length == 64),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_WithLargePayloadAndStorageService_OffloadsToMinIoAndStoresPointer()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.CallerIdentity.Returns("batch-generator");
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(projectId));
        var largeJson = new string('A', DocumentAuditProcessor.MaxInlinePayloadSizeBytes + 500);
        var genId = Guid.NewGuid();

        // Act
        await CreateSut(_storageService).LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), largeJson, "outputs/large.pdf", OutputFormat.Pdf, 50000, 300);

        // Assert
        await _storageService.Received(1).UploadAsync(
            Arg.Is(StorageBuckets.AuditPayloads),
            Arg.Is<string>(key => key.StartsWith($"audit/{projectId:N}/") && key.EndsWith(".json")),
            Arg.Any<Stream>(),
            Arg.Is("application/json"),
            Arg.Any<CancellationToken>());

        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l =>
                l.Id == genId &&
                l.InputData != null &&
                l.InputData.StartsWith("[OFFLOADED:audit/") &&
                l.PayloadHashSha256 != null &&
                l.PayloadHashSha256.Value.Length == 64),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_WithLargePayloadAndNullStorageService_FallsBackToInlineStorage()
    {
        // Arrange
        _projectContext.CallerIdentity.Returns("direct-service");
        var largeJson = new string('B', DocumentAuditProcessor.MaxInlinePayloadSizeBytes + 100);
        var genId = Guid.NewGuid();

        // Act (null storage service)
        await CreateSut(storage: null).LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), largeJson, "outputs/fallback.pdf", OutputFormat.Pdf, 10000, 120);

        // Assert
        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l =>
                l.Id == genId &&
                l.InputData == largeJson &&
                l.PayloadHashSha256 != null &&
                l.PayloadHashSha256.Value.Length == 64),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogValidationFailureAsync_WithLargePayload_OffloadsToMinIo()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.CallerIdentity.Returns("api-gateway");
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(projectId));
        var largeJson = new string('C', DocumentAuditProcessor.MaxInlinePayloadSizeBytes + 200);

        // Act
        await CreateSut(_storageService).LogValidationFailureAsync(
            Guid.NewGuid(), Guid.NewGuid(), largeJson, OutputFormat.Pdf, 40, "Invalid schema");

        // Assert
        await _storageService.Received(1).UploadAsync(
            Arg.Is(StorageBuckets.AuditPayloads),
            Arg.Is<string>(key => key.StartsWith($"audit/{projectId:N}/")),
            Arg.Any<Stream>(),
            Arg.Is("application/json"),
            Arg.Any<CancellationToken>());

        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l =>
                l.Status == GenerationStatus.ValidationFailed &&
                l.InputData != null &&
                l.InputData.StartsWith("[OFFLOADED:audit/")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_WithoutProjectId_ThrowsTenantContextNotResolvedException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.GetRequiredProjectId().Returns(_ => throw new TenantContextNotResolvedException());

        // Act
        var act = async () => await CreateSut().LogSuccessAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "outputs/test.pdf", OutputFormat.Pdf, 100, 10);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
    }
}

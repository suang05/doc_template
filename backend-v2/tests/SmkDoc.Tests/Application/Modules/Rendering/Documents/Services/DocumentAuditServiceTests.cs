using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditServiceTests
{
    private readonly IGenerationLogRepository _logRepo = Substitute.For<IGenerationLogRepository>();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly TimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private DocumentAuditService CreateSut(IStorageService? storage = null) => new(
        _logRepo, _context, storage, null, _timeProvider);

    [Fact]
    public async Task LogValidationFailureAsync_ShouldAddFailedLogWithoutPrematureCommit()
    {
        // Arrange
        _context.CallerApp.Returns("crm-app");
        _context.ProjectId.Returns(Guid.NewGuid());

        // Act
        await CreateSut().LogValidationFailureAsync(
            Guid.NewGuid(), Guid.NewGuid(), "{}", OutputFormat.Pdf, 50, "Schema violation");

        // Assert
        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l => l.Status == GenerationStatus.ValidationFailed && l.CallerApp == "crm-app"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_ShouldAddSuccessLogWithoutPrematureCommit()
    {
        // Arrange
        _context.CallerApp.Returns("pos-app");
        _context.ProjectId.Returns(Guid.NewGuid());
        var genId = Guid.NewGuid();

        // Act
        await CreateSut().LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), "{}", "outputs/receipt.pdf", OutputFormat.Pdf, 1024, 150);

        // Assert
        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l => l.Id == genId && l.Status == GenerationStatus.Success && l.OutputKey == "outputs/receipt.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_WithSmallPayload_StoresPayloadInlineAndComputesSha256()
    {
        // Arrange
        _context.CallerApp.Returns("web-portal");
        _context.ProjectId.Returns(Guid.NewGuid());
        var smallJson = "{\"customerId\": 42, \"name\": \"Acme Corp\"}";
        var genId = Guid.NewGuid();

        // Act
        await CreateSut(_storageService).LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), smallJson, "outputs/invoice.pdf", OutputFormat.Pdf, 2048, 80);

        // Assert
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _logRepo.Received(1).AddAsync(
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
        _context.CallerApp.Returns("batch-generator");
        _context.ProjectId.Returns(projectId);
        var largeJson = new string('A', DocumentAuditService.MaxInlinePayloadSizeBytes + 500);
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

        await _logRepo.Received(1).AddAsync(
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
        _context.CallerApp.Returns("direct-service");
        _context.ProjectId.Returns(Guid.NewGuid());
        var largeJson = new string('B', DocumentAuditService.MaxInlinePayloadSizeBytes + 100);
        var genId = Guid.NewGuid();

        // Act (null storage service)
        await CreateSut(storage: null).LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), largeJson, "outputs/fallback.pdf", OutputFormat.Pdf, 10000, 120);

        // Assert
        await _logRepo.Received(1).AddAsync(
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
        _context.CallerApp.Returns("api-gateway");
        _context.ProjectId.Returns(projectId);
        var largeJson = new string('C', DocumentAuditService.MaxInlinePayloadSizeBytes + 200);

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

        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l =>
                l.Status == GenerationStatus.ValidationFailed &&
                l.InputData != null &&
                l.InputData.StartsWith("[OFFLOADED:audit/")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_WithoutProjectId_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        _context.ProjectId.Returns((Guid?)null);

        // Act
        var act = async () => await CreateSut().LogSuccessAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "outputs/test.pdf", OutputFormat.Pdf, 100, 10);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage("*project context*");
    }
}

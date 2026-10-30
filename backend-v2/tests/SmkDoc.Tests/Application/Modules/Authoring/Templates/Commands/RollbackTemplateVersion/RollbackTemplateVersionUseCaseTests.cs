using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

public sealed class RollbackTemplateVersionUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly ProjectId _projectId = ProjectId.New();

    public RollbackTemplateVersionUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
        _projectContext.CallerIdentity.Returns("studio-admin");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private RollbackTemplateVersionUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        _projectContext,
        _unitOfWork,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenTargetVersionExists_PerformsZeroCopyRollbackRestoringSchemaAndMappings()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var template = Template.Create(
            _projectId,
            TemplateName.Create("Sales Contract"),
            TemplateSlug.Create("sales-contract"),
            "Legal",
            now);

        var v1Mapping = FieldMapping.Create(
            template.Id,
            "buyerName",
            "buyer.fullName",
            "ชื่อผู้ซื้อ",
            true,
            1,
            now,
            DataSourceType.Json,
            "N/A",
            null);
        var v1Snapshot = FieldMappingSnapshotSerializer.Serialize([v1Mapping]);

        var v1Key = StorageBuckets.TemplateVersionKey(_projectId, template.Id, 1, ".html");
        var v1 = template.CreateDraftVersion(1, v1Key, TemplateFormat.Html, "author1", now, "v1");
        v1.UpdateDataSchema("{\"title\":\"v1\"}", "{\"buyer\":{\"fullName\":\"Alice\"}}", now);
        v1.UpdateMappingsSnapshot(v1Snapshot, now);
        template.PublishVersion(v1, now);

        var v2Mapping = FieldMapping.Create(
            template.Id,
            "brokenField",
            "broken.path",
            "Broken",
            false,
            1,
            now,
            DataSourceType.Json);
        template.ReplaceFieldMappings([v2Mapping], now);
        var v2Key = StorageBuckets.TemplateVersionKey(_projectId, template.Id, 2, ".html");
        var v2 = template.CreateDraftVersion(2, v2Key, TemplateFormat.Html, "author2", now, "v2");
        v2.UpdateDataSchema("{\"title\":\"v2\"}", "{}", now);
        v2.UpdateMappingsSnapshot(FieldMappingSnapshotSerializer.Serialize([v2Mapping]), now);
        template.PublishVersion(v2, now);

        _templateRepo.GetByIdWithDetailsAsync(template.Id, Arg.Any<CancellationToken>())
            .Returns(template);
        _storageService.ExistsAsync(StorageBuckets.Templates, v1Key, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        int nextVersionNumber = await CreateSut().ExecuteAsync(new RollbackTemplateVersionCommand(template.Id.Value, 1));

        // Assert
        nextVersionNumber.Should().Be(3);

        // Zero-copy storage invariant: NEVER download or re-upload bytes
        await _storageService.Received(1).ExistsAsync(StorageBuckets.Templates, v1Key, Arg.Any<CancellationToken>());
        await _storageService.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Previous Published version archived, v3 published reusing v1 immutable StorageKey + metadata
        v2.Status.Should().Be(TemplateVersionStatus.Archived);
        var v3 = template.Versions.Single(v => v.Version == 3);
        v3.Status.Should().Be(TemplateVersionStatus.Published);
        v3.StorageKey.Should().Be(v1Key);
        v3.DataSchema.Should().Be("{\"title\":\"v1\"}");
        v3.SamplePayload.Should().Be("{\"buyer\":{\"fullName\":\"Alice\"}}");
        v3.MappingsSnapshot.Should().Be(v1Snapshot);
        template.CurrentVersionId.Should().Be(v3.Id);

        // Active FieldMappings restored from v1 snapshot
        template.FieldMappings.Should().ContainSingle();
        template.FieldMappings.Single().Placeholder.Should().Be("buyerName");
        template.FieldMappings.Single().SourcePath.Should().Be("buyer.fullName");

        _templateRepo.Received(1).Update(template);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenStorageAssetMissing_ShouldThrowNotFoundException()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var template = Template.Create(
            _projectId,
            TemplateName.Create("Sales Contract"),
            TemplateSlug.Create("sales-contract"),
            "Legal",
            now);
        var v1 = template.CreateDraftVersion(1, "templates/missing/v1.html", TemplateFormat.Html, "author", now, "v1");
        template.PublishVersion(v1, now);

        _templateRepo.GetByIdWithDetailsAsync(template.Id, Arg.Any<CancellationToken>())
            .Returns(template);
        _storageService.ExistsAsync(StorageBuckets.Templates, "templates/missing/v1.html", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(new RollbackTemplateVersionCommand(template.Id.Value, 1));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Storage asset*");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(new RollbackTemplateVersionCommand(Guid.NewGuid(), 1));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Queries;

public sealed class FontQueriesUseCaseTests
{
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public FontQueriesUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
    }

    [Fact]
    public async Task ListFonts_WhenResolved_ListsDynamicTenantPrefixPlusBuiltInDefault()
    {
        // Arrange
        var prefix = $"fonts/{_projectId.Value:N}/";
        _storage.ListObjectsAsync(StorageBuckets.Fonts, prefix, Arg.Any<CancellationToken>())
            .Returns([$"fonts/{_projectId.Value:N}/Kanit-Bold.ttf", $"fonts/{_projectId.Value:N}/Prompt-Regular.woff2"]);

        var sut = new ListFontsUseCase(_storage, _projectContext);

        // Act
        var fonts = await sut.ExecuteAsync(new ListFontsQuery());

        // Assert
        fonts.Should().BeEquivalentTo(["Kanit-Bold.ttf", "Prompt-Regular.woff2", "Sarabun-Regular.ttf"]);
    }

    [Fact]
    public async Task GetFontBase64_WhenTenantFontExists_DownloadsFromTenantKey()
    {
        // Arrange
        var tenantKey = $"fonts/{_projectId.Value:N}/Kanit-Bold.ttf";
        byte[] fontBytes = [0x00, 0x01, 0x00, 0x00, 0xAA, 0xBB];
        _storage.ExistsAsync(StorageBuckets.Fonts, tenantKey, Arg.Any<CancellationToken>()).Returns(true);
        _storage.DownloadAsync(StorageBuckets.Fonts, tenantKey, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(fontBytes));

        var sut = new GetFontBase64UseCase(_storage, _projectContext);

        // Act
        var base64 = await sut.ExecuteAsync(new GetFontBase64Query("Kanit-Bold.ttf"));

        // Assert
        base64.Should().Be(Convert.ToBase64String(fontBytes));
    }

    [Fact]
    public async Task GetFontBase64_WhenPathTraversalSupplied_ThrowsDomainValidationException()
    {
        // Arrange
        var sut = new GetFontBase64UseCase(_storage, _projectContext);

        // Act
        Func<Task> act = () => sut.ExecuteAsync(new GetFontBase64Query("../other-tenant/Secret.ttf"));

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*path traversal*");
    }

    [Fact]
    public async Task FontQueries_WhenProjectUnresolved_ThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var listSut = new ListFontsUseCase(_storage, _projectContext);
        var getSut = new GetFontBase64UseCase(_storage, _projectContext);

        // Act & Assert
        await listSut.Invoking(s => s.ExecuteAsync(new ListFontsQuery())).Should().ThrowAsync<ForbiddenException>();
        await getSut.Invoking(s => s.ExecuteAsync(new GetFontBase64Query("Sarabun-Regular.ttf"))).Should().ThrowAsync<ForbiddenException>();
    }
}

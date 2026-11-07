using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    public ListApiKeysUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(TestConstants.DefaultProjectId));
    }

    private ListApiKeysUseCase CreateSut() => new(_apiKeyRepo, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolvedAndNotSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.SystemRole.Returns(SystemRole.Member);

        // Act
        var act = () => CreateSut().ExecuteAsync(new ListApiKeysQuery());

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Active project context is required*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAmbientProjectResolved_ReturnsProjectScopedApiKeysWithMaskedFormat()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var now = TestConstants.BaselineTime;
        ApiKey key1 = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("ERP Key")
            .WithCallerApp("erp")
            .WithKeyHash(new Sha256Hash(new string('1', 60) + "abcd"))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(now);
        ApiKey key2 = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("CRM Key")
            .WithCallerApp("crm")
            .WithKeyHash(new Sha256Hash(new string('2', 60) + "cdef"))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(now);

        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { key1, key2 });

        // Act
        List<ApiKeyResultDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery());

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("ERP Key");
        result[0].MaskedKey.Should().Be("smk_erp_****abcd");
        result[1].Name.Should().Be("CRM Key");
        result[1].MaskedKey.Should().Be("smk_crm_****cdef");
        await _apiKeyRepo.Received(1).ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminAndNoProjectSpecified_ReturnsAllKeys()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.SystemRole.Returns(SystemRole.SuperAdmin);

        var key = new ApiKeyBuilder().WithName("Global Key").WithCallerApp("global").Build();
        _apiKeyRepo.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { key });

        // Act
        List<ApiKeyResultDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery());

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("Global Key");
        await _apiKeyRepo.Received(1).ListAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectHasNoApiKeys_ReturnsEmptyList()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        // Act
        List<ApiKeyResultDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery());

        // Assert
        result.Should().BeEmpty();
        await _apiKeyRepo.Received(1).ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>());
    }
}

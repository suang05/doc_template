using FluentAssertions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Contexts;
using Xunit;

namespace SmkDoc.Tests.Application.Common.Context;

public sealed class CurrentProjectContextTests
{
    private readonly CurrentProjectContext _sut = new();

    [Fact]
    public void UninitializedContext_ShouldHaveEmptyProjectIdAndUnresolvedState()
    {
        // Assert
        _sut.ProjectId.Should().Be(ProjectId.Empty);
        _sut.IsResolved.Should().BeFalse();
        _sut.CallerIdentity.Should().BeNull();
    }

    [Fact]
    public void SetProject_WithValidProjectId_ShouldResolveAndStoreIdentity()
    {
        // Arrange
        var projectId = ProjectId.New();
        const string callerIdentity = "erp-system";

        // Act
        _sut.SetProject(projectId, callerIdentity);

        // Assert
        _sut.IsResolved.Should().BeTrue();
        _sut.ProjectId.Should().Be(projectId);
        _sut.CallerIdentity.Should().Be(callerIdentity);
    }

    [Fact]
    public void SetProject_WithoutCallerIdentity_ShouldResolveWithNullCallerIdentity()
    {
        // Arrange
        var projectId = ProjectId.New();

        // Act
        _sut.SetProject(projectId);

        // Assert
        _sut.IsResolved.Should().BeTrue();
        _sut.ProjectId.Should().Be(projectId);
        _sut.CallerIdentity.Should().BeNull();
    }

    [Fact]
    public void SetProject_WhenAlreadyResolved_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var projectId1 = ProjectId.New();
        var projectId2 = ProjectId.New();
        _sut.SetProject(projectId1, "initial-caller");

        // Act
        var act = () => _sut.SetProject(projectId2, "second-caller");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been resolved*");
    }

    [Fact]
    public void SetProject_WithEmptyProjectId_ShouldThrowDomainValidationException()
    {
        // Act
        var act = () => _sut.SetProject(ProjectId.Empty, "caller");

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void InterfaceSegregation_ReadInterface_ShouldExposeOnlyReadMembers()
    {
        // Arrange
        ICurrentProjectContext reader = _sut;
        var projectId = ProjectId.New();

        // Act
        ((ICurrentProjectContextSetter)_sut).SetProject(projectId, "audit-user");

        // Assert
        reader.IsResolved.Should().BeTrue();
        reader.ProjectId.Should().Be(projectId);
        reader.CallerIdentity.Should().Be("audit-user");
    }

    [Fact]
    public void SetActor_ChannelA_M2MApiKey_ShouldHydrateApiKeyIdAndCallerIdentity()
    {
        // Arrange
        var apiKeyId = ApiKeyId.New();

        // Act
        _sut.SetActor(apiKeyId: apiKeyId, callerIdentity: "erp-system");

        // Assert
        _sut.IsResolved.Should().BeFalse();
        _sut.ApiKeyId.Should().Be(apiKeyId);
        _sut.UserId.Should().BeNull();
        _sut.SystemRole.Should().BeNull();
        _sut.CallerIdentity.Should().Be("erp-system");
    }

    [Fact]
    public void SetActor_ChannelB_WebStudioUser_ShouldHydrateUserIdSystemRoleAndCallerIdentity()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        _sut.SetActor(
            userId: userId,
            systemRole: SmkDoc.Domain.Enums.SystemRole.SuperAdmin,
            callerIdentity: $"User:{userId.Value:N}");

        // Assert
        _sut.IsResolved.Should().BeFalse();
        _sut.UserId.Should().Be(userId);
        _sut.SystemRole.Should().Be(SmkDoc.Domain.Enums.SystemRole.SuperAdmin);
        _sut.ApiKeyId.Should().BeNull();
        _sut.CallerIdentity.Should().Be($"User:{userId.Value:N}");
    }
}

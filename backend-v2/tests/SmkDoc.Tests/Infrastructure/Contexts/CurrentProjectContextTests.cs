using FluentAssertions;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Contexts;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Contexts;

public sealed class CurrentProjectContextTests
{
    private static CurrentProjectContext CreateSut() => new();

    [Fact]
    public void ProjectId_WhenUnresolved_ShouldThrowTenantContextRequiredException()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act & Assert
        sut.IsResolved.Should().BeFalse();
        var act = () => _ = sut.ProjectId;
        act.Should().Throw<TenantContextRequiredException>()
            .WithMessage("*active tenant project context is required*");
    }

    [Fact]
    public void OptionalProjectId_WhenUnresolved_ShouldReturnNull()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act & Assert
        sut.OptionalProjectId.Should().BeNull();
    }

    [Fact]
    public void SetProject_WhenValidProjectIdProvided_ShouldResolveBothProperties()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var expectedId = ProjectId.New();

        // Step 2: Act
        sut.SetProject(expectedId, "test-caller");

        // Step 3: Assert
        sut.IsResolved.Should().BeTrue();
        sut.ProjectId.Should().Be(expectedId);
        sut.OptionalProjectId.Should().Be(expectedId);
        sut.CallerIdentity.Should().Be("test-caller");
    }

    [Fact]
    public void SetProject_WhenAlreadyResolved_ShouldThrowInvalidOperationException()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        sut.SetProject(ProjectId.New());

        // Step 2: Act
        var act = () => sut.SetProject(ProjectId.New());

        // Step 3: Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been resolved*");
    }

    [Fact]
    public void SetProject_WhenEmptyProjectIdProvided_ShouldThrowDomainValidationException()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        var act = () => sut.SetProject(ProjectId.Empty);

        // Step 3: Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void SetActor_ShouldHydrateActorMetadata()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var userId = new UserId(Guid.NewGuid());
        var apiKeyId = new ApiKeyId(Guid.NewGuid());

        // Step 2: Act
        sut.SetActor(
            userId: userId,
            systemRole: SystemRole.SuperAdmin,
            apiKeyId: apiKeyId,
            callerIdentity: "custom-actor");

        // Step 3: Assert
        sut.UserId.Should().Be(userId);
        sut.SystemRole.Should().Be(SystemRole.SuperAdmin);
        sut.ApiKeyId.Should().Be(apiKeyId);
        sut.CallerIdentity.Should().Be("custom-actor");
    }
}

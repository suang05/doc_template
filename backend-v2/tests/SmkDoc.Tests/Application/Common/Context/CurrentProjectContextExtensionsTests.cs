using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Extensions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Application.Common.Context;

public sealed class CurrentProjectContextExtensionsTests
{
    [Fact]
    public void EnsureResolved_WhenContextIsNull_ThrowsForbiddenException()
    {
        // Arrange
        ICurrentProjectContext? context = null;

        // Act
        var act = () => context.EnsureResolved("Custom failure");

        // Assert
        act.Should().Throw<ForbiddenException>()
            .WithMessage("Custom failure");
    }

    [Fact]
    public void EnsureResolved_WhenContextIsNotResolved_ThrowsForbiddenException_WithDefaultMessage()
    {
        // Arrange
        var context = Substitute.For<ICurrentProjectContext>();
        context.IsResolved.Returns(false);

        // Act
        var act = () => context.EnsureResolved();

        // Assert
        act.Should().Throw<ForbiddenException>()
            .WithMessage("Project context is not resolved.");
    }

    [Fact]
    public void EnsureResolved_WhenContextIsNotResolved_ThrowsForbiddenException_WithCustomMessage()
    {
        // Arrange
        var context = Substitute.For<ICurrentProjectContext>();
        context.IsResolved.Returns(false);

        // Act
        var act = () => context.EnsureResolved("Operation requires tenant.");

        // Assert
        act.Should().Throw<ForbiddenException>()
            .WithMessage("Operation requires tenant.");
    }

    [Fact]
    public void EnsureResolved_WhenContextIsResolved_ReturnsProjectId()
    {
        // Arrange
        var expectedId = ProjectId.New();
        var context = Substitute.For<ICurrentProjectContext>();
        context.IsResolved.Returns(true);
        context.ProjectId.Returns(expectedId);

        // Act
        var actualId = context.EnsureResolved();

        // Assert
        actualId.Should().Be(expectedId);
    }
}


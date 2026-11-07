using NSubstitute;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;

public sealed class RemoveUserUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private RemoveUserUseCase CreateSut() => new(
        _roleRepo,
        _uow,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(Guid.NewGuid()));

        var command = new RemoveUserCommand(Guid.Empty, Guid.Empty);
        var pipeline = new ValidatingUseCaseDecorator<RemoveUserCommand>(
            CreateSut(),
            [new RemoveUserCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _roleRepo.DidNotReceive().GetAsync(Arg.Any<ProjectId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidInput_RemovesUserSuccessfully()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        var command = new RemoveUserCommand(userId, currentUserId);

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);

        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        _roleRepo.Received(1).Remove(roleEntry);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLastAdmin_ThrowsConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsAdmin()
            .Build();

        var command = new RemoveUserCommand(userId, currentUserId);

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);
        _roleRepo.CountAdminsAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _roleRepo.DidNotReceive().Remove(Arg.Any<UserProjectRole>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotInProject_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var command = new RemoveUserCommand(userId, currentUserId);

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns((UserProjectRole?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchMatches_RemovesUserSuccessfully()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        var token = roleEntry.CreatedAt.ToUnixTimeMilliseconds().ToString();
        var command = new RemoveUserCommand(userId, currentUserId, $"\"{token}\"");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);

        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        _roleRepo.Received(1).Remove(roleEntry);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchMismatches_ThrowsPreconditionFailedException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        var command = new RemoveUserCommand(userId, currentUserId, "\"99999999\"");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<PreconditionFailedException>();
        _roleRepo.DidNotReceive().Remove(roleEntry);
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

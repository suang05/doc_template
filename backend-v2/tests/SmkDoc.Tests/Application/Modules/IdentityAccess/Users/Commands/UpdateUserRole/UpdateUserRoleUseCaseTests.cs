using NSubstitute;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public sealed class UpdateUserRoleUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private UpdateUserRoleUseCase CreateSut() => new(
        _roleRepo,
        _userRepo,
        _uow,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(Guid.NewGuid()));

        var command = new UpdateUserRoleCommand(Guid.Empty, "");
        var pipeline = new ValidatingUseCaseDecorator<UpdateUserRoleCommand, UserResultDto>(
            CreateSut(),
            [new UpdateUserRoleCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _roleRepo.DidNotReceive().GetAsync(Arg.Any<ProjectId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _userRepo.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidInput_UpdatesRoleSuccessfully()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        User user = aUser.Active().WithId(userId);
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        var command = new UpdateUserRoleCommand(userId, "Developer");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);
        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        roleEntry.Role.Should().Be(RoleType.Developer);
        result.Should().NotBeNull();
        result.Role.Should().Be("Developer");
        result.Id.Should().Be(userId);
        _roleRepo.Received(1).Update(roleEntry);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLastAdminDemoted_ThrowsConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsAdmin()
            .Build();

        var command = new UpdateUserRoleCommand(userId, "Viewer");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);
        _roleRepo.CountAdminsAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotInProject_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var command = new UpdateUserRoleCommand(userId, "Developer");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns((UserProjectRole?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchMatches_UpdatesRoleSuccessfully()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        User user = aUser.Active().WithId(userId);
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        var token = roleEntry.CreatedAt.ToUnixTimeMilliseconds().ToString();
        var command = new UpdateUserRoleCommand(userId, "Developer", $"\"{token}\"");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);
        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        roleEntry.Role.Should().Be(RoleType.Developer);
        result.ConcurrencyToken.Should().Be(token);
        _roleRepo.Received(1).Update(roleEntry);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfMatchMismatches_ThrowsPreconditionFailedException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        var command = new UpdateUserRoleCommand(userId, "Developer", "\"99999999\"");

        _roleRepo.GetAsync(new ProjectId(projectId), new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleEntry);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<PreconditionFailedException>();
        _roleRepo.DidNotReceive().Update(roleEntry);
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

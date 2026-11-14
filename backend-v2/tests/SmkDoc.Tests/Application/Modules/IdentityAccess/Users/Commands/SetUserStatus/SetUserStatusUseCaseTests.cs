using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public sealed class SetUserStatusUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly UserId _actorSuperAdminId = UserId.New();

    public SetUserStatusUseCaseTests()
    {
        _projectContext.SystemRole.Returns(SystemRole.SuperAdmin);
        _projectContext.UserId.Returns(_actorSuperAdminId);
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private SetUserStatusUseCase CreateSut() => new(
        _userRepo,
        _roleRepo,
        _uow,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        var command = new SetUserStatusCommand(Guid.Empty, false);
        var pipeline = new ValidatingUseCaseDecorator<SetUserStatusCommand, UserResultDto>(
            CreateSut(),
            [new SetUserStatusCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _userRepo.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.SystemRole.Returns(SystemRole.Member);
        var command = new SetUserStatusCommand(Guid.NewGuid(), true);

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _userRepo.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        var command = new SetUserStatusCommand(userId.Value, true);
        _userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenActivating_UpdatesUserStatusToActive()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(projectId));

        var userId = new UserId(Guid.NewGuid());
        User user = UserBuilder.AUser().AsInactive().WithId(userId.Value);
        _userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var roleEntry = new UserProjectRoleBuilder()
            .ForUser(userId)
            .InProject(projectId)
            .AsDeveloper()
            .Build();

        _roleRepo.GetAsync(new ProjectId(projectId), userId, Arg.Any<CancellationToken>())
            .Returns(roleEntry);

        var command = new SetUserStatusCommand(userId.Value, true);

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        user.IsActive.Should().BeTrue();
        _userRepo.Received(1).Update(user);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        result.IsActive.Should().BeTrue();
        result.Role.Should().Be("Developer");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminDeactivatesThemselves_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        User selfUser = UserBuilder.AUser().WithId(_actorSuperAdminId.Value).WithSystemRole(SystemRole.SuperAdmin);
        _userRepo.GetByIdAsync(_actorSuperAdminId, Arg.Any<CancellationToken>())
            .Returns(selfUser);

        var command = new SetUserStatusCommand(_actorSuperAdminId.Value, false);

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("SELF_DEACTIVATION_FORBIDDEN");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeactivatingLastSuperAdmin_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var targetSuperAdminId = UserId.New();
        User targetSuperAdmin = UserBuilder.AUser().WithId(targetSuperAdminId.Value).WithSystemRole(SystemRole.SuperAdmin);
        _userRepo.GetByIdAsync(targetSuperAdminId, Arg.Any<CancellationToken>())
            .Returns(targetSuperAdmin);
        _userRepo.CountActiveSuperAdminsAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var command = new SetUserStatusCommand(targetSuperAdminId.Value, false);

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("LAST_SUPERADMIN_FORBIDDEN");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeactivatingNonSuperAdmin_MarksUserInactive()
    {
        // Arrange
        var targetUserId = UserId.New();
        User targetUser = UserBuilder.AUser().WithId(targetUserId.Value).WithSystemRole(SystemRole.Member);
        _userRepo.GetByIdAsync(targetUserId, Arg.Any<CancellationToken>())
            .Returns(targetUser);

        var command = new SetUserStatusCommand(targetUserId.Value, false);

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        targetUser.IsActive.Should().BeFalse();
        _userRepo.Received(1).Update(targetUser);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        result.IsActive.Should().BeFalse();
    }
}

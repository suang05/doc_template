using NSubstitute;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public sealed class SetUserStatusUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepo = Substitute.For<IRefreshTokenRepository>();
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
        _refreshTokenRepo,
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
        var act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _userRepo.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerIsNotSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.SystemRole.Returns(SystemRole.Member);
        var command = new SetUserStatusCommand(Guid.NewGuid(), false);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only global SuperAdmin*");
        _userRepo.DidNotReceive().Update(Arg.Any<User>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminDeactivatesAnotherMember_DeactivatesAndRevokesRefreshTokens()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        User user = aUser.Active();
        var userId = user.Id;
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsDeveloper()
            .Build();

        var command = new SetUserStatusCommand(userId.Value, false);

        _roleRepo.GetAsync(new ProjectId(projectId), userId, Arg.Any<CancellationToken>())
            .Returns(roleEntry);
        _userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        user.IsActive.Should().BeFalse();
        result.Should().NotBeNull();
        result.IsActive.Should().BeFalse();
        result.Role.Should().Be("Developer");
        await _refreshTokenRepo.Received(1).RevokeAllByUserIdAsync(userId, _timeProvider.GetUtcNow(), Arg.Any<CancellationToken>());
        _userRepo.Received(1).Update(user);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminAttemptsSelfDeactivation_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        User selfUser = aUser.Active().WithId(_actorSuperAdminId.Value).WithSystemRole(SystemRole.SuperAdmin);
        var command = new SetUserStatusCommand(_actorSuperAdminId.Value, false);

        _userRepo.GetByIdAsync(_actorSuperAdminId, Arg.Any<CancellationToken>())
            .Returns(selfUser);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("SELF_DEACTIVATION_FORBIDDEN");
        await _refreshTokenRepo.DidNotReceive().RevokeAllByUserIdAsync(Arg.Any<UserId>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeactivatingLastActiveSuperAdmin_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        User targetSuperAdmin = aUser.Active().WithSystemRole(SystemRole.SuperAdmin);
        var command = new SetUserStatusCommand(targetSuperAdmin.Id.Value, false);

        _userRepo.GetByIdAsync(targetSuperAdmin.Id, Arg.Any<CancellationToken>())
            .Returns(targetSuperAdmin);
        _userRepo.CountActiveSuperAdminsAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("LAST_SUPERADMIN_FORBIDDEN");
        await _refreshTokenRepo.DidNotReceive().RevokeAllByUserIdAsync(Arg.Any<UserId>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new SetUserStatusCommand(userId, true);

        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _userRepo.DidNotReceive().Update(Arg.Any<User>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

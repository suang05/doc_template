using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public sealed class InviteUserUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IValidator<InviteUserCommand> _validator = Substitute.For<IValidator<InviteUserCommand>>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private InviteUserUseCase CreateSut() => new(
        _userRepo,
        _roleRepo,
        _passwordHasher,
        _uow,
        _validator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        var command = new InviteUserCommand(Guid.Empty, "invalid-email", "", "", "", "Viewer");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Email", "Invalid email")]));

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _roleRepo.DidNotReceive().AddAsync(Arg.Any<UserProjectRole>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_CreatesUserAndRole()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var email = "new@test.com";
        var password = "Pass@1234";
        var roleStr = "Developer";
        var command = new InviteUserCommand(projectId, email, password, "New", "User", roleStr);

        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _userRepo.GetByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _passwordHasher.HashPassword(password)
            .Returns("hash");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        await _userRepo.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _roleRepo.Received(1).AddAsync(Arg.Any<UserProjectRole>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        result.Email.Should().Be(email);
        result.Role.Should().Be("Developer");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserAlreadyMember_ThrowsConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        User existingUser = aUser.Active().WithEmail("exist@test.com");
        var existingRole = new UserProjectRoleBuilder()
            .ForUser(existingUser.Id)
            .InProject(projectId)
            .AsViewer()
            .Build();

        var command = new InviteUserCommand(projectId, "exist@test.com", "Pass@1234", "", "", "Viewer");

        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _userRepo.GetByEmailAsync("exist@test.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);
        _roleRepo.GetAsync(new ProjectId(projectId), existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingRole);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

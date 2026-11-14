using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public sealed class InviteUserUseCaseTests
{
    private readonly IUserAccountProcessor _userAccountProcessor = Substitute.For<IUserAccountProcessor>();
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private InviteUserUseCase CreateSut() => new(
        _userAccountProcessor,
        _roleRepo,
        _uow,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingPersistence()
    {
        // Arrange
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(Guid.NewGuid()));

        var command = new InviteUserCommand("invalid-email", "", "", "", "Viewer");
        var pipeline = new ValidatingUseCaseDecorator<InviteUserCommand, UserResultDto>(
            CreateSut(),
            [new InviteUserCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _userAccountProcessor.DidNotReceive().GetOrRegisterUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _roleRepo.DidNotReceive().AddAsync(Arg.Any<UserProjectRole>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_CreatesUserAndRole()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(projectId));

        var email = "new@test.com";
        var password = "Pass@1234";
        var roleStr = "Developer";
        var command = new InviteUserCommand(email, password, "New", "User", roleStr);

        User newUser = aUser.Active().WithEmail(email);
        _userAccountProcessor.GetOrRegisterUserAsync(email, password, "New", "User", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(newUser);

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
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
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(projectId));

        User existingUser = aUser.Active().WithEmail("exist@test.com");
        var existingRole = new UserProjectRoleBuilder()
            .ForUser(existingUser.Id)
            .InProject(projectId)
            .AsViewer()
            .Build();

        var command = new InviteUserCommand("exist@test.com", "Pass@1234", "", "", "Viewer");

        _userAccountProcessor.GetOrRegisterUserAsync("exist@test.com", "Pass@1234", "", "", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(existingUser);
        _roleRepo.GetAsync(new ProjectId(projectId), existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingRole);

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _roleRepo.DidNotReceive().AddAsync(Arg.Any<UserProjectRole>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

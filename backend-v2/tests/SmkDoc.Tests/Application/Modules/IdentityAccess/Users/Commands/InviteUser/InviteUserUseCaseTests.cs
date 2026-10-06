using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.InviteUser;

public class InviteUserUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_CreatesUserAndRole()
    {
        var projectId = Guid.NewGuid();
        var email = "new@test.com";
        var password = "Pass@1234";
        var roleStr = "Developer";

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _fixture.PasswordHasher.Setup(p => p.HashPassword(password)).Returns("hash");

        var useCase = _fixture.BuildInviteUserUseCase();
        var command = new InviteUserCommand(projectId, email, password, "New", "User", roleStr);
        var result = await useCase.ExecuteAsync(command);

        _fixture.UserRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.RoleRepo.Verify(r => r.AddAsync(It.IsAny<UserProjectRole>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.Email.Should().Be(email);
        result.Role.Should().Be("Developer");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserAlreadyMember_ThrowsConflictException()
    {
        var projectId = Guid.NewGuid();
        var existingUser = new UserBuilder().WithEmail("exist@test.com").Build();
        var existingRole = new UserProjectRoleBuilder()
            .ForUser(existingUser.Id)
            .InProject(projectId)
            .AsViewer()
            .Build();

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync("exist@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        var useCase = _fixture.BuildInviteUserUseCase();
        var command = new InviteUserCommand(projectId, "exist@test.com", "Pass@1234", "", "", "Viewer");
        var act = () => useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }
}

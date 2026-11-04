using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;

public sealed class TransferProjectOwnerUseCaseTests
{
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUserProjectRoleRepository _userRoleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public TransferProjectOwnerUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private TransferProjectOwnerUseCase CreateSut() =>
        new(_projectRepo, _userRepo, _userRoleRepo, _unitOfWork, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();
        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns((Project?)null);

        var command = new TransferProjectOwnerCommand(projectId, newOwnerId);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{projectId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewOwnerNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();
        var project = ProjectBuilder.AProject().WithId(projectId).Build();

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);
        _userRepo.GetByIdAsync(new UserId(newOwnerId), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var command = new TransferProjectOwnerCommand(projectId, newOwnerId);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{newOwnerId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewOwnerInactive_ThrowsDomainValidationException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();
        var project = ProjectBuilder.AProject().WithId(projectId).Build();
        var inactiveUser = UserBuilder.AUser().WithId(newOwnerId).Build();
        inactiveUser.Deactivate(_timeProvider.GetUtcNow());

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);
        _userRepo.GetByIdAsync(new UserId(newOwnerId), Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        var command = new TransferProjectOwnerCommand(projectId, newOwnerId);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_DowngradesPriorAdminAndAssignsNewAdminRole()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var priorOwnerId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        var project = ProjectBuilder.AProject().WithId(projectId).Build();
        var newOwner = UserBuilder.AUser().WithId(newOwnerId).Build();

        var priorRole = UserProjectRole.Create(new UserId(priorOwnerId), new ProjectId(projectId), RoleType.Admin, _timeProvider.GetUtcNow());
        var existingRoles = new List<UserProjectRole> { priorRole };

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);
        _userRepo.GetByIdAsync(new UserId(newOwnerId), Arg.Any<CancellationToken>())
            .Returns(newOwner);
        _userRoleRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(existingRoles);

        var command = new TransferProjectOwnerCommand(projectId, newOwnerId);

        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        priorRole.Role.Should().Be(RoleType.Developer);
        _userRoleRepo.Received(1).Update(priorRole);
        await _userRoleRepo.Received(1).AddAsync(
            Arg.Is<UserProjectRole>(r => r.UserId == new UserId(newOwnerId) && r.Role == RoleType.Admin),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetUserAlreadyHasRole_UpdatesExistingRoleToAdmin()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var priorOwnerId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        var project = ProjectBuilder.AProject().WithId(projectId).Build();
        var newOwner = UserBuilder.AUser().WithId(newOwnerId).Build();

        var priorRole = UserProjectRole.Create(new UserId(priorOwnerId), new ProjectId(projectId), RoleType.Admin, _timeProvider.GetUtcNow());
        var targetRole = UserProjectRole.Create(new UserId(newOwnerId), new ProjectId(projectId), RoleType.Developer, _timeProvider.GetUtcNow());
        var existingRoles = new List<UserProjectRole> { priorRole, targetRole };

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);
        _userRepo.GetByIdAsync(new UserId(newOwnerId), Arg.Any<CancellationToken>())
            .Returns(newOwner);
        _userRoleRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(existingRoles);

        var command = new TransferProjectOwnerCommand(projectId, newOwnerId);

        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        priorRole.Role.Should().Be(RoleType.Developer);
        targetRole.Role.Should().Be(RoleType.Admin);
        _userRoleRepo.Received(1).Update(priorRole);
        _userRoleRepo.Received(1).Update(targetRole);
        await _userRoleRepo.DidNotReceive().AddAsync(Arg.Any<UserProjectRole>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}


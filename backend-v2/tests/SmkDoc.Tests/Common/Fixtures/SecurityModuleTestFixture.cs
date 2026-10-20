using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Common.Fixtures;

/// <summary>
/// SSoT test fixture for Security / IdentityAccess module use cases, providing pre-configured mocks
/// and declarative helpers (RULE-TEST-007).
/// </summary>
public class SecurityModuleTestFixture
{
    public Mock<IUserRepository> UserRepo { get; } = new();
    public Mock<IPasswordHasher> PasswordHasher { get; } = new();
    public Mock<IJwtTokenGenerator> JwtGenerator { get; } = new();
    public Mock<IUserWorkspaceQueryService> WorkspaceQueryService { get; } = new();
    public Mock<IRefreshTokenRepository> TokenRepo { get; } = new();
    public Mock<IUnitOfWork> Uow { get; } = new();
    public Mock<IExecutionContext> ExecutionContext { get; } = new();
    public FakeTimeProvider TimeProvider { get; } = TestConstants.CreateFakeClock();
    public Mock<ISecurityAuditService> AuditService { get; } = new();
    public LoginCommandValidator DefaultLoginValidator { get; } = new();
    public RefreshTokenCommandValidator DefaultRefreshTokenValidator { get; } = new();

    public SecurityModuleTestFixture()
    {
        Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    public LoginUseCase BuildLoginUseCase(
        IValidator<LoginCommand>? validator = null,
        Guid? executionContextProjectId = null)
    {
        ExecutionContext.Setup(x => x.ProjectId).Returns(executionContextProjectId);

        return new(
            UserRepo.Object,
            PasswordHasher.Object,
            JwtGenerator.Object,
            WorkspaceQueryService.Object,
            validator ?? DefaultLoginValidator,
            TokenRepo.Object,
            Uow.Object,
            TimeProvider,
            ExecutionContext.Object,
            AuditService.Object);
    }

    public RefreshTokenUseCase BuildRefreshTokenUseCase(
        Guid? executionContextProjectId = null,
        IValidator<RefreshTokenCommand>? validator = null)
    {
        ExecutionContext.Setup(x => x.ProjectId).Returns(executionContextProjectId);

        return new(
            TokenRepo.Object,
            UserRepo.Object,
            JwtGenerator.Object,
            WorkspaceQueryService.Object,
            Uow.Object,
            validator ?? DefaultRefreshTokenValidator,
            TimeProvider,
            ExecutionContext.Object,
            AuditService.Object);
    }

    public GetCurrentUserProfileUseCase BuildGetCurrentUserProfileUseCase() =>
        new(UserRepo.Object, WorkspaceQueryService.Object, ExecutionContext.Object);

    public void GivenUserWithWorkspace(User user, UserWorkspaceContextDto workspaceContext)
    {
        UserRepo.Setup(r => r.GetByEmailAsync(user.Email.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
    }

    public void GivenActiveRefreshToken(RefreshTokenEntity token)
    {
        TokenRepo.Setup(r => r.GetByHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
    }
}

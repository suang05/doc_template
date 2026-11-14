using SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.EventHandlers;

public sealed class UserDeactivatedEventHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly ISecurityAuditService _auditService = Substitute.For<ISecurityAuditService>();

    private UserDeactivatedEventHandler CreateSut() =>
        new(_refreshTokenRepo, _auditService);

    [Fact]
    public async Task HandleAsync_WhenUserDeactivatedEventRaised_RevokesAllSessionsAndLogsAudit()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var domainEvent = new UserDeactivatedDomainEvent(userId, now);

        // Act
        var sut = CreateSut();
        await sut.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _auditService.Received(1).LogUserDeactivated(userId.Value);
        await _refreshTokenRepo.Received(1).RevokeAllByUserIdAsync(userId, now, Arg.Any<CancellationToken>());
    }
}


using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmkDoc.Infrastructure.Security;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Security;

public class SecurityAuditServiceTests
{
    private readonly Mock<ILogger<SecurityAuditService>> _loggerMock = new();

    private SecurityAuditService CreateSut() => new(_loggerMock.Object);

    [Fact]
    public void LogLoginSuccess_DispatchesInformationLog()
    {
        // Arrange
        var sut = CreateSut();
        var userId = Guid.NewGuid();
        var email = "admin@example.com";
        var projectId = Guid.NewGuid();

        // Act
        sut.LogLoginSuccess(userId, email, projectId);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(email) && v.ToString()!.Contains(userId.ToString())),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogLoginFailure_DispatchesWarningLog()
    {
        // Arrange
        var sut = CreateSut();
        var email = "attacker@example.com";
        var reason = "InvalidPassword";

        // Act
        sut.LogLoginFailure(email, reason);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(email) && v.ToString()!.Contains(reason)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogTokenRotated_DispatchesInformationLog()
    {
        // Arrange
        var sut = CreateSut();
        var userId = Guid.NewGuid();

        // Act
        sut.LogTokenRotated(userId);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(userId.ToString())),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogTokenReuseDetected_DispatchesCriticalLog()
    {
        // Arrange
        var sut = CreateSut();
        var userId = Guid.NewGuid();

        // Act
        sut.LogTokenReuseDetected(userId);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(userId.ToString())),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

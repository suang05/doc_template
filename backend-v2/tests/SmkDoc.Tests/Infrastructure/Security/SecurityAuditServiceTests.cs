using Microsoft.Extensions.Logging;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Tests.Infrastructure.Security;

public sealed class SecurityAuditServiceTests
{
    private readonly ILogger<SecurityAuditService> _logger = Substitute.For<ILogger<SecurityAuditService>>();

    private SecurityAuditService CreateSut() => new(_logger);

    private void AssertLogged(LogLevel level, params string[] messageParts)
    {
        var match = _logger.ReceivedCalls().Any(call =>
        {
            if (call.GetMethodInfo().Name != "Log") return false;
            var args = call.GetArguments();
            if (args.Length < 3 || args[0] is not LogLevel l || l != level) return false;
            var stateStr = args[2]?.ToString() ?? string.Empty;
            return messageParts.All(part => stateStr.Contains(part));
        });

        match.Should().BeTrue($"Expected log level '{level}' containing [{string.Join(", ", messageParts)}]");
    }

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
        AssertLogged(LogLevel.Information, email, userId.ToString());
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
        AssertLogged(LogLevel.Warning, email, reason);
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
        AssertLogged(LogLevel.Information, userId.ToString());
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
        AssertLogged(LogLevel.Critical, userId.ToString());
    }
}

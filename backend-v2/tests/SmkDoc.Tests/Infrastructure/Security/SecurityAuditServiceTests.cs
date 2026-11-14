using Microsoft.Extensions.Logging;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Tests.Infrastructure.Security;

public sealed class SecurityAuditServiceTests
{
    private readonly CapturingLogger _logger = new();

    private SecurityAuditService CreateSut() => new(_logger);

    private void AssertLogged(LogLevel level, params string[] messageParts)
    {
        var match = _logger.Entries.Any(entry =>
            entry.Level == level &&
            messageParts.All(part => entry.Message.Contains(part, StringComparison.Ordinal)));

        var diag = string.Join(" | ", _logger.Entries.Select(e => $"{e.Level}({e.EventId.Name}): {e.Message}"));
        match.Should().BeTrue($"Expected log level '{level}' containing [{string.Join(", ", messageParts)}], got entries: [{diag}]");
    }

    private sealed class CapturingLogger : ILogger<SecurityAuditService>
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId, formatter(state, exception)));
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

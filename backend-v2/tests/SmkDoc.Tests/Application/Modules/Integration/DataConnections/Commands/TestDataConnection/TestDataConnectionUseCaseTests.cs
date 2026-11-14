using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public sealed class TestDataConnectionUseCaseTests
{
    private readonly ISqlExecutorService _sqlExecutor = Substitute.For<ISqlExecutorService>();

    private TestDataConnectionUseCase CreateSut() => new(_sqlExecutor);

    [Fact]
    public async Task ExecuteAsync_WhenQuerySucceeds_ShouldReturnTrue()
    {
        // Arrange
        var command = new TestDataConnectionCommand("PostgreSQL", "valid-conn");
        _sqlExecutor.ExecuteQueryAsJsonAsync("PostgreSQL", "valid-conn", "SELECT 1", null, Arg.Any<CancellationToken>())
            .Returns("[{\"1\": 1}]");

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Connection successful.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNetworkFails_ShouldReturnFalse()
    {
        // Arrange
        var command = new TestDataConnectionCommand("PostgreSQL", "invalid-conn");
        _sqlExecutor.ExecuteQueryAsJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Network timeout"));

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Connection failed.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSsrfForbidden_ShouldRethrowForbiddenException()
    {
        // Arrange
        var command = new TestDataConnectionCommand("PostgreSQL", "Host=127.0.0.1;Database=smk");
        _sqlExecutor.ExecuteQueryAsJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ForbiddenException("SSRF target blocked"));

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("SSRF target blocked");
    }
}

using SmkDoc.Application.Common.Interfaces;
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
        _sqlExecutor.ExecuteQueryAsJsonAsync("PostgreSQL", "valid-conn", "SELECT 1", null)
            .Returns("[{\"1\": 1}]");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryFails_ShouldReturnFalse()
    {
        // Arrange
        var command = new TestDataConnectionCommand("PostgreSQL", "invalid-conn");
        _sqlExecutor.ExecuteQueryAsJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>?>())
            .ThrowsAsync(new InvalidOperationException("Network timeout"));

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().BeFalse();
    }
}

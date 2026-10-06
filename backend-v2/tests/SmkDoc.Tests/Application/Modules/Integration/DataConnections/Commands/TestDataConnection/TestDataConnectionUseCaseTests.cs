using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public class TestDataConnectionUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private TestDataConnectionUseCase CreateSut() =>
        new(_fixture.SqlExecutor.Object);

    [Fact]
    public async Task ExecuteAsync_WhenQuerySucceeds_ShouldReturnTrue()
    {
        var command = new TestDataConnectionCommand("PostgreSQL", "valid-conn");
        _fixture.SqlExecutor.Setup(s => s.ExecuteQueryAsJsonAsync("PostgreSQL", "valid-conn", "SELECT 1", null))
            .ReturnsAsync("[{\"1\": 1}]");

        var result = await CreateSut().ExecuteAsync(command);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryFails_ShouldReturnFalse()
    {
        var command = new TestDataConnectionCommand("PostgreSQL", "invalid-conn");
        _fixture.SqlExecutor.Setup(s => s.ExecuteQueryAsJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>?>()))
            .ThrowsAsync(new Exception("Network timeout"));

        var result = await CreateSut().ExecuteAsync(command);

        result.Should().BeFalse();
    }
}

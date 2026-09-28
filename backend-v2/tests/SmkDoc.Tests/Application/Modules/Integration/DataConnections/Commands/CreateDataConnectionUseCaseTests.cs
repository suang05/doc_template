using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands;

public class CreateDataConnectionUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private CreateDataConnectionUseCase CreateSut() =>
        new(_fixture.ConnectionRepo.Object, _fixture.UnitOfWork.Object, _fixture.DataProtection.Object);

    [Fact]
    public async Task ExecuteAsync_ShouldEncryptAndPersistConnection()
    {
        var command = new CreateDataConnectionCommand("Warehouse", "PostgreSQL", "Host=wh.local;Pass=secret");

        var result = await CreateSut().ExecuteAsync(command);

        result.Name.Should().Be("Warehouse");
        result.Provider.Should().Be("PostgreSQL");

        _fixture.ConnectionRepo.Verify(r => r.AddAsync(It.Is<DataConnection>(c =>
            c.Name == "Warehouse" &&
            c.Provider == "PostgreSQL" &&
            c.EncryptedConnectionString == "enc:Host=wh.local;Pass=secret"), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

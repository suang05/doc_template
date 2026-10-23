using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionUseCaseTests
{
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public CreateDataConnectionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _dataProtection.Encrypt(Arg.Any<string>()).Returns(x => $"enc:{x.Arg<string>()}");
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
    }

    private CreateDataConnectionUseCase CreateSut() => new(
        _connectionRepo,
        _unitOfWork,
        _dataProtection,
        projectContext: _projectContext);

    [Fact]
    public async Task ExecuteAsync_ShouldEncryptAndPersistConnection()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("Warehouse", "PostgreSQL", "Host=wh.local;Pass=secret");

        // Act
        DataConnectionDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Name.Should().Be("Warehouse");
        result.Provider.Should().Be("PostgreSQL");

        await _connectionRepo.Received(1).AddAsync(
            Arg.Is<DataConnection>(c =>
                c.Name.Value == "Warehouse" &&
                c.Provider == DatabaseProvider.PostgreSQL &&
                c.EncryptedConnectionString == "enc:Host=wh.local;Pass=secret"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}

using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections;
using SmkDoc.Domain.Entities;
using Xunit;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets;

public class DataConnectionUseCaseTests
{
    private readonly Mock<IDataConnectionRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IDataProtectionService> _dataProtectionMock = new();
    private readonly Mock<ISqlExecutorService> _sqlExecutorMock = new();

    private DataConnectionUseCase CreateSut() =>
        new(_repositoryMock.Object, _unitOfWorkMock.Object, _dataProtectionMock.Object, _sqlExecutorMock.Object);

    // ── GetAllAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllConnectionsWithoutExposingConnectionString()
    {
        var conn1 = new DataConnection("Postgres Main", "PostgreSQL", "enc_secret_1");
        var conn2 = new DataConnection("SqlServer Legacy", "SqlServer", "enc_secret_2");

        _repositoryMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DataConnection> { conn1, conn2 });

        var sut = CreateSut();
        var results = await sut.GetAllAsync();

        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Postgres Main");
        results[0].Provider.Should().Be("PostgreSQL");
        results[1].Name.Should().Be("SqlServer Legacy");
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDto_WhenFound()
    {
        var id = Guid.NewGuid();
        var conn = new DataConnection("Production DB", "PostgreSQL", "enc_secret", id: id);

        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conn);

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Name.Should().Be("Production DB");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(id);

        result.Should().BeNull();
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ShouldEncryptConnectionStringAndPersist()
    {
        var dto = new CreateDataConnectionDto
        {
            Name = "Reporting DB",
            Provider = "PostgreSQL",
            ConnectionString = "Host=myhost;Password=secret"
        };

        _dataProtectionMock.Setup(p => p.Encrypt("Host=myhost;Password=secret"))
            .Returns("encrypted_hash_123");

        DataConnection? savedEntity = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<DataConnection>(), It.IsAny<CancellationToken>()))
            .Callback<DataConnection, CancellationToken>((entity, _) => savedEntity = entity)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut();
        var result = await sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Reporting DB");
        result.Provider.Should().Be("PostgreSQL");

        savedEntity.Should().NotBeNull();
        savedEntity!.EncryptedConnectionString.Should().Be("encrypted_hash_123");
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndReEncrypt_WhenNewConnectionStringProvided()
    {
        var id = Guid.NewGuid();
        var existing = new DataConnection("Old Name", "PostgreSQL", "old_enc", id: id);

        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _dataProtectionMock.Setup(p => p.Encrypt("new_plain_conn"))
            .Returns("new_enc_conn");

        _unitOfWorkMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updateDto = new UpdateDataConnectionDto
        {
            Name = "New Name",
            Provider = "SqlServer",
            ConnectionString = "new_plain_conn"
        };

        var sut = CreateSut();
        var result = await sut.UpdateAsync(id, updateDto);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Provider.Should().Be("SqlServer");
        existing.EncryptedConnectionString.Should().Be("new_enc_conn");
        existing.UpdatedAt.Should().NotBeNull();
        _repositoryMock.Verify(r => r.Update(existing), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveExistingConnectionString_WhenConnectionStringIsEmpty()
    {
        var id = Guid.NewGuid();
        var existing = new DataConnection("DB", "PostgreSQL", "keep_this_enc", id: id);

        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var updateDto = new UpdateDataConnectionDto
        {
            Name = "DB Renamed",
            Provider = "PostgreSQL",
            ConnectionString = "" // empty, do not change
        };

        var sut = CreateSut();
        var result = await sut.UpdateAsync(id, updateDto);

        result.Should().NotBeNull();
        existing.EncryptedConnectionString.Should().Be("keep_this_enc");
        _dataProtectionMock.Verify(p => p.Encrypt(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenEntityNotFound()
    {
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var sut = CreateSut();
        var result = await sut.UpdateAsync(id, new UpdateDataConnectionDto { Name = "X" });

        result.Should().BeNull();
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAndReturnTrue_WhenExists()
    {
        var id = Guid.NewGuid();
        var existing = new DataConnection("To Delete", "sql", "enc_delete", id: id);

        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _unitOfWorkMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut();
        var result = await sut.DeleteAsync(id);

        result.Should().BeTrue();
        _repositoryMock.Verify(r => r.Remove(existing), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var sut = CreateSut();
        var result = await sut.DeleteAsync(id);

        result.Should().BeFalse();
        _repositoryMock.Verify(r => r.Remove(It.IsAny<DataConnection>()), Times.Never);
    }

    // ── TestConnectionAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrue_WhenSqlExecutorSucceeds()
    {
        var dto = new TestDataConnectionDto
        {
            Provider = "PostgreSQL",
            ConnectionString = "Host=localhost;Database=test;"
        };

        _sqlExecutorMock.Setup(s => s.ExecuteQueryAsJsonAsync("PostgreSQL", "Host=localhost;Database=test;", "SELECT 1", null))
            .ReturnsAsync("[{\"1\": 1}]");

        var sut = CreateSut();
        var result = await sut.TestConnectionAsync(dto);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnFalse_WhenSqlExecutorThrows()
    {
        var dto = new TestDataConnectionDto
        {
            Provider = "PostgreSQL",
            ConnectionString = "invalid_conn"
        };

        _sqlExecutorMock.Setup(s => s.ExecuteQueryAsJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>?>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var sut = CreateSut();
        var result = await sut.TestConnectionAsync(dto);

        result.Should().BeFalse();
    }
}

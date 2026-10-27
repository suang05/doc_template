using FluentAssertions;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Validators;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.DataConnections.Validators;

public sealed class DataConnectionCommandValidatorsTests
{
    private readonly CreateDataConnectionCommandValidator _createValidator = new();
    private readonly UpdateDataConnectionCommandValidator _updateValidator = new();

    [Fact]
    public async Task CreateValidator_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("MainWarehouse", "PostgreSQL", "Host=localhost;Database=db;");

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateValidator_WithEmptyName_ShouldFail(string name)
    {
        // Arrange
        var command = new CreateDataConnectionCommand(name, "PostgreSQL", "Host=localhost;");

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDataConnectionCommand.Name));
    }

    [Fact]
    public async Task CreateValidator_WithNameExceedingMaxLength_ShouldFail()
    {
        // Arrange
        var longName = new string('A', 101);
        var command = new CreateDataConnectionCommand(longName, "PostgreSQL", "Host=localhost;");

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDataConnectionCommand.Name));
    }

    [Theory]
    [InlineData("InvalidProvider")]
    [InlineData("Sqlite")]
    [InlineData("")]
    public async Task CreateValidator_WithInvalidProvider_ShouldFail(string provider)
    {
        // Arrange
        var command = new CreateDataConnectionCommand("Conn1", provider, "Host=localhost;");

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDataConnectionCommand.Provider));
    }

    [Fact]
    public async Task CreateValidator_WithEmptyConnectionString_ShouldFail()
    {
        // Arrange
        var command = new CreateDataConnectionCommand("Conn1", "PostgreSQL", "");

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDataConnectionCommand.ConnectionString));
    }

    [Fact]
    public async Task UpdateValidator_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new UpdateDataConnectionCommand(Guid.NewGuid(), "Warehouse_Updated", "SqlServer", "Server=tcp:sql.local;");

        // Act
        var result = await _updateValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateValidator_WithEmptyConnectionString_ShouldPass_ToPreserveExistingSecret()
    {
        // Arrange
        var command = new UpdateDataConnectionCommand(Guid.NewGuid(), "Warehouse_Renamed", "PostgreSQL", "");

        // Act
        var result = await _updateValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateValidator_WithEmptyId_ShouldFail()
    {
        // Arrange
        var command = new UpdateDataConnectionCommand(Guid.Empty, "Warehouse", "PostgreSQL", "Host=localhost;");

        // Act
        var result = await _updateValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateDataConnectionCommand.Id));
    }
}

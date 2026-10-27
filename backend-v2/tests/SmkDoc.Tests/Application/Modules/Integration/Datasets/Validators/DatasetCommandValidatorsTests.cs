using FluentAssertions;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Validators;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Validators;

public sealed class DatasetCommandValidatorsTests
{
    private readonly CreateDatasetCommandValidator _createValidator = new();
    private readonly UpdateDatasetCommandValidator _updateValidator = new();

    [Fact]
    public async Task CreateValidator_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateDatasetCommand("SalesQ1", "Q1 Sales dataset", Guid.NewGuid(), "SELECT * FROM sales", 120);

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
        var command = new CreateDatasetCommand(name, null, Guid.NewGuid(), "SELECT 1", 0);

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatasetCommand.Name));
    }

    [Fact]
    public async Task CreateValidator_WithEmptyDataConnectionId_ShouldFail()
    {
        // Arrange
        var command = new CreateDatasetCommand("DS1", null, Guid.Empty, "SELECT 1", 0);

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatasetCommand.DataConnectionId));
    }

    [Fact]
    public async Task CreateValidator_WithEmptySqlQuery_ShouldFail()
    {
        // Arrange
        var command = new CreateDatasetCommand("DS1", null, Guid.NewGuid(), "", 0);

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatasetCommand.SqlQuery));
    }

    [Fact]
    public async Task CreateValidator_WithNegativeCacheSeconds_ShouldFail()
    {
        // Arrange
        var command = new CreateDatasetCommand("DS1", null, Guid.NewGuid(), "SELECT 1", -10);

        // Act
        var result = await _createValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatasetCommand.CacheSeconds));
    }

    [Fact]
    public async Task UpdateValidator_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new UpdateDatasetCommand(Guid.NewGuid(), "SalesUpdated", "Updated description", Guid.NewGuid(), "SELECT 2", 300);

        // Act
        var result = await _updateValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateValidator_WithEmptyId_ShouldFail()
    {
        // Arrange
        var command = new UpdateDatasetCommand(Guid.Empty, "SalesUpdated", null, Guid.NewGuid(), "SELECT 2", 0);

        // Act
        var result = await _updateValidator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateDatasetCommand.Id));
    }
}

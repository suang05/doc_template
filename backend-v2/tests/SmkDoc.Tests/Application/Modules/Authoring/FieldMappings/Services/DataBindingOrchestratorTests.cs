using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Services;

public sealed class DataBindingOrchestratorTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly IFieldMappingApplicatorService _applicator = Substitute.For<IFieldMappingApplicatorService>();

    private DataBindingOrchestrator CreateSut() =>
        new(_datasetRepo, _connectionRepo, _dataProtection, _applicator);

    [Fact]
    public async Task AssemblePayloadAsync_WithNoMappingsAndNoDatasets_ShouldReturnRawCallerParameters()
    {
        // Arrange
        var template = new TemplateBuilder()
            .WithId(Guid.NewGuid())
            .WithProjectId(Guid.NewGuid())
            .WithName("Simple")
            .WithSlug("simple")
            .Build();

        var callerJson = "{\"invoiceId\":1001,\"customer\":\"Acme Corp\"}";
        var parameters = JsonDocument.Parse(callerJson).RootElement;
        var sut = CreateSut();

        // Act
        var result = await sut.AssemblePayloadAsync(template, parameters);

        // Assert
        result.Should().Be(callerJson);
        await _applicator.DidNotReceive().ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<IEnumerable<FieldMapping>>(),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssemblePayloadAsync_WithNoMappingsAndUndefinedParameters_ShouldReturnEmptyObject()
    {
        // Arrange
        var template = new TemplateBuilder()
            .WithId(Guid.NewGuid())
            .WithProjectId(Guid.NewGuid())
            .WithName("Empty")
            .WithSlug("empty")
            .Build();

        var sut = CreateSut();

        // Act
        var result = await sut.AssemblePayloadAsync(template, default);

        // Assert
        result.Should().Be("{}");
    }

    [Fact]
    public async Task AssemblePayloadAsync_WithMappings_ShouldDelegateToApplicator()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("WithMappings")
            .WithSlug("with-mappings")
            .Build();

        var mapping = new FieldMappingBuilder()
            .WithTemplateId(templateId)
            .WithPlaceholder("client")
            .WithSourcePath("customer.name")
            .WithDataSourceType(DataSourceType.Json)
            .Build();

        template.ReplaceFieldMappings([mapping], TestConstants.BaselineTime);

        _applicator.ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<IEnumerable<FieldMapping>>(),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>())
            .Returns("{\"client\":\"Acme Corp\"}");

        var callerJson = "{\"customer\":{\"name\":\"Acme Corp\"}}";
        var parameters = JsonDocument.Parse(callerJson).RootElement;
        var sut = CreateSut();

        // Act
        var result = await sut.AssemblePayloadAsync(template, parameters);

        // Assert
        result.Should().Be("{\"client\":\"Acme Corp\"}");
        await _applicator.Received(1).ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Is<IEnumerable<FieldMapping>>(m => m.Count() == 1),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>());
    }
}

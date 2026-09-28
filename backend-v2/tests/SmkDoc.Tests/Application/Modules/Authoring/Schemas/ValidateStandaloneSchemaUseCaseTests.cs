using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.DTOs.Schemas;
using SmkDoc.Application.UseCases.Schemas;
using SmkDoc.Infrastructure.Schema;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Schemas;

public class ValidateStandaloneSchemaUseCaseTests
{
    private readonly ValidateStandaloneSchemaUseCase _useCase;

    public ValidateStandaloneSchemaUseCaseTests()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = new JsonSchemaValidationService(cache);
        _useCase = new ValidateStandaloneSchemaUseCase(validator);
    }

    [Fact]
    public void Execute_WithValidSchemaAndPayload_ReturnsValidResult()
    {
        const string schemaJson = """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "type": "object",
              "required": ["doc_no", "amount"],
              "properties": {
                "doc_no": { "type": "string" },
                "amount": { "type": "number", "minimum": 1 }
              }
            }
            """;

        const string payloadJson = """
            {
              "doc_no": "INV-001",
              "amount": 1500.50
            }
            """;

        using var schemaDoc = JsonDocument.Parse(schemaJson);
        using var payloadDoc = JsonDocument.Parse(payloadJson);

        var command = new ValidateStandaloneSchemaCommand(schemaDoc.RootElement, payloadDoc.RootElement);
        var result = _useCase.Execute(command);

        result.Valid.Should().BeTrue();
        result.Message.Should().Contain("successfully");
        result.Errors.Should().BeNull();
    }

    [Fact]
    public void Execute_WithInvalidPayload_ReturnsInvalidResultWithErrors()
    {
        const string schemaJson = """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "type": "object",
              "required": ["doc_no", "tax_id"],
              "properties": {
                "doc_no": { "type": "string" },
                "tax_id": { "type": "string", "minLength": 13, "maxLength": 13 }
              }
            }
            """;

        const string payloadJson = """
            {
              "doc_no": "INV-002",
              "tax_id": "1234"
            }
            """;

        using var schemaDoc = JsonDocument.Parse(schemaJson);
        using var payloadDoc = JsonDocument.Parse(payloadJson);

        var command = new ValidateStandaloneSchemaCommand(schemaDoc.RootElement, payloadDoc.RootElement);
        var result = _useCase.Execute(command);

        result.Valid.Should().BeFalse();
        result.Errors.Should().NotBeNullOrEmpty();
        result.Errors!.Should().Contain(e => e.Field == "/tax_id");
    }

    [Fact]
    public void Execute_WithEmptySchema_ReturnsValidResult()
    {
        using var schemaDoc = JsonDocument.Parse("{}");
        using var payloadDoc = JsonDocument.Parse("{\"any\": \"data\"}");

        var command = new ValidateStandaloneSchemaCommand(schemaDoc.RootElement, payloadDoc.RootElement);
        var result = _useCase.Execute(command);

        result.Valid.Should().BeTrue();
    }
}

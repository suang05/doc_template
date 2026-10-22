using FluentAssertions;
using System.Text.Json;
using SmkDoc.Infrastructure.Schema;
using Xunit;
using Microsoft.Extensions.Caching.Memory;

namespace SmkDoc.Tests.Infrastructure.Schema;

/// <summary>
/// Unit tests for <see cref="JsonSchemaValidationService"/>.
/// Validates Draft-07 schema evaluation logic: required fields, type checking,
/// pattern matching, minimum constraints, and format keywords.
/// </summary>
public sealed class JsonSchemaValidationServiceTests
{
    private readonly JsonSchemaValidationService _sut = new(new MemoryCache(new MemoryCacheOptions()));

    // ─── Shared test schema ───────────────────────────────────────────────────

    private const string InvoiceSchema = """
                                         {
                                           "$schema": "http://json-schema.org/draft-07/schema#",
                                           "type": "object",
                                           "required": ["doc_no", "issue_date", "customer", "items"],
                                           "properties": {
                                             "doc_no": {
                                               "type": "string",
                                               "pattern": "^INV-\\d{4}-\\d+$"
                                             },
                                             "issue_date": {
                                               "type": "string",
                                               "format": "date"
                                             },
                                             "customer": {
                                               "type": "object",
                                               "required": ["name", "tax_id"],
                                               "properties": {
                                                 "name": { "type": "string", "minLength": 1 },
                                                 "tax_id": { "type": "string", "pattern": "^\\d{13}$" }
                                               }
                                             },
                                             "items": {
                                               "type": "array",
                                               "minItems": 1,
                                               "items": {
                                                 "type": "object",
                                                 "required": ["name", "qty", "price"],
                                                 "properties": {
                                                   "name":  { "type": "string" },
                                                   "qty":   { "type": "number", "minimum": 1 },
                                                   "price": { "type": "number", "minimum": 0 }
                                                 }
                                               }
                                             }
                                           }
                                         }
                                         """;

    private const string ValidPayload = """
                                        {
                                          "doc_no":     "INV-2026-001",
                                          "issue_date": "2026-09-20",
                                          "customer": {
                                            "name":   "บริษัท ABC จำกัด",
                                            "tax_id": "1234567890123"
                                          },
                                          "items": [
                                            { "name": "บริการออกแบบ", "qty": 1, "price": 5000.00 }
                                          ]
                                        }
                                        """;

    // ─── Pass cases ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithValidPayload_ShouldReturnIsValidTrue()
    {
        var result = _sut.Validate(InvoiceSchema, ValidPayload);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ─── Schema Validation Failures ──────────────────────────────────────────

    [Theory]
    [InlineData("""{"issue_date": "2026-09-20", "customer": { "name": "ABC", "tax_id": "1234567890123" }, "items": [{ "name": "X", "qty": 1, "price": 100 }]}""")] // missing doc_no
    [InlineData("""{"doc_no": "INV-2026-002", "issue_date": "2026-09-20", "customer": { "name": "ABC" }, "items": [{ "name": "X", "qty": 1, "price": 100 }]}""")] // missing customer.tax_id
    [InlineData("""{"doc_no": "INV-2026-003", "issue_date": "2026-09-20", "customer": { "name": "XYZ Co.", "tax_id": "12345" }, "items": [{ "name": "Service", "qty": 1, "price": 200 }]}""")] // tax_id pattern mismatch
    [InlineData("""{"doc_no": "WRONG-FORMAT", "issue_date": "2026-09-20", "customer": { "name": "ABC", "tax_id": "1234567890123" }, "items": [{ "name": "X", "qty": 1, "price": 50 }]}""")] // doc_no pattern mismatch
    [InlineData("""{"doc_no": "INV-2026-004", "issue_date": "2026-09-20", "customer": { "name": "ABC", "tax_id": "1234567890123" }, "items": [{ "name": "Refund", "qty": 1, "price": -100 }]}""")] // negative price
    [InlineData("""{"doc_no": "INV-2026-005", "issue_date": "2026-09-20", "customer": { "name": "ABC", "tax_id": "1234567890123" }, "items": [{ "name": "Widget", "qty": 0, "price": 10 }]}""")] // zero qty
    [InlineData("""{"doc_no": "INV-2026-006", "issue_date": "2026-09-20", "customer": { "name": "ABC", "tax_id": "1234567890123" }, "items": []}""")] // empty items
    public void Validate_WithInvalidInvoicePayload_ReturnsErrors(string invalidPayload)
    {
        var result = _sut.Validate(InvoiceSchema, invalidPayload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // ─── Malformed inputs ─────────────────────────────────────────────────────

    [Fact]
    public void Validate_MalformedPayloadJson_ShouldReturnErrorGracefully()
    {
        var result = _sut.Validate(InvoiceSchema, "{ this is not json }");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Field.Should().Be("/");
    }

    [Fact]
    public void Validate_MalformedSchemaJson_ShouldReturnErrorGracefully()
    {
        var result = _sut.Validate("{ bad schema *** }", ValidPayload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Field.Should().Be("/");
    }

    // ─── No schema (pass-through) ─────────────────────────────────────────────

    [Fact]
    public void Validate_EmptySchemaAllowsEverything()
    {
        // An empty object schema {} accepts anything
        var result = _sut.Validate("{}", ValidPayload);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithJsonElements_ShouldValidateCorrectly()
    {
        using var schemaDoc = JsonDocument.Parse(InvoiceSchema);
        using var validDoc = JsonDocument.Parse(ValidPayload);

        var result = _sut.Validate(schemaDoc.RootElement, validDoc.RootElement);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}

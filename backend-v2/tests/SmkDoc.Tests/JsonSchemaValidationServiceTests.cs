using FluentAssertions;
using SmkDoc.Infrastructure.Schema;
using Xunit;

namespace SmkDoc.Tests;

/// <summary>
/// Unit tests for <see cref="JsonSchemaValidationService"/>.
/// Validates Draft-07 schema evaluation logic: required fields, type checking,
/// pattern matching, minimum constraints, and format keywords.
/// </summary>
public class JsonSchemaValidationServiceTests
{
    private readonly JsonSchemaValidationService _sut = new();

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

    // ─── Required field failures ──────────────────────────────────────────────

    [Fact]
    public void Validate_MissingTopLevelRequired_ShouldReturnErrors()
    {
        const string data = """
            {
              "issue_date": "2026-09-20",
              "customer": { "name": "ABC", "tax_id": "1234567890123" },
              "items": [{ "name": "X", "qty": 1, "price": 100 }]
            }
            """;

        var result = _sut.Validate(InvoiceSchema, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_MissingNestedRequired_ShouldReturnErrors()
    {
        const string data = """
            {
              "doc_no":     "INV-2026-002",
              "issue_date": "2026-09-20",
              "customer":   { "name": "ABC" },
              "items": [{ "name": "X", "qty": 1, "price": 100 }]
            }
            """;

        // customer.tax_id is missing
        var result = _sut.Validate(InvoiceSchema, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // ─── Pattern mismatch ────────────────────────────────────────────────────

    [Fact]
    public void Validate_TaxIdPatternMismatch_ShouldReturnErrors()
    {
        const string data = """
            {
              "doc_no":     "INV-2026-003",
              "issue_date": "2026-09-20",
              "customer": { "name": "XYZ Co.", "tax_id": "12345" },
              "items": [{ "name": "Service", "qty": 1, "price": 200 }]
            }
            """;

        var result = _sut.Validate(InvoiceSchema, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_DocNoPatternMismatch_ShouldReturnErrors()
    {
        const string data = """
            {
              "doc_no":     "WRONG-FORMAT",
              "issue_date": "2026-09-20",
              "customer": { "name": "ABC", "tax_id": "1234567890123" },
              "items": [{ "name": "X", "qty": 1, "price": 50 }]
            }
            """;

        var result = _sut.Validate(InvoiceSchema, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // ─── Minimum violations ───────────────────────────────────────────────────

    [Fact]
    public void Validate_NegativePrice_ShouldReturnErrors()
    {
        const string data = """
            {
              "doc_no":     "INV-2026-004",
              "issue_date": "2026-09-20",
              "customer": { "name": "ABC", "tax_id": "1234567890123" },
              "items": [{ "name": "Refund", "qty": 1, "price": -100 }]
            }
            """;

        var result = _sut.Validate(InvoiceSchema, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_ZeroQty_ShouldReturnErrors()
    {
        const string data = """
            {
              "doc_no":     "INV-2026-005",
              "issue_date": "2026-09-20",
              "customer": { "name": "ABC", "tax_id": "1234567890123" },
              "items": [{ "name": "Widget", "qty": 0, "price": 10 }]
            }
            """;

        var result = _sut.Validate(InvoiceSchema, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_EmptyItemsArray_ShouldReturnErrors()
    {
        const string data = """
            {
              "doc_no":     "INV-2026-006",
              "issue_date": "2026-09-20",
              "customer": { "name": "ABC", "tax_id": "1234567890123" },
              "items": []
            }
            """;

        var result = _sut.Validate(InvoiceSchema, data);

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
        result.Errors[0].PropertyPath.Should().Be("/");
    }

    [Fact]
    public void Validate_MalformedSchemaJson_ShouldReturnErrorGracefully()
    {
        var result = _sut.Validate("{ bad schema *** }", ValidPayload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].PropertyPath.Should().Be("/");
    }

    // ─── No schema (pass-through) ─────────────────────────────────────────────

    [Fact]
    public void Validate_EmptySchemaAllowsEverything()
    {
        // An empty object schema {} accepts anything
        var result = _sut.Validate("{}", ValidPayload);

        result.IsValid.Should().BeTrue();
    }
}

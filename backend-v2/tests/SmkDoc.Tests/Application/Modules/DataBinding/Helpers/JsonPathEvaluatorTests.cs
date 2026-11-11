using System.Text.Json;
using FluentAssertions;
using SmkDoc.Application.Modules.DataBinding.Helpers;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.DataBinding.Helpers;

public sealed class JsonPathEvaluatorTests
{
    [Fact]
    public void ResolvePath_WithSimpleProperty_ReturnsStringValue()
    {
        using var doc = JsonDocument.Parse("""{"name":"Alice","age":30,"active":true}""");

        JsonPathEvaluator.ResolvePath(doc.RootElement, "name").Should().Be("Alice");
        JsonPathEvaluator.ResolvePath(doc.RootElement, "age").Should().Be("30");
        JsonPathEvaluator.ResolvePath(doc.RootElement, "active").Should().Be("true");
    }

    [Fact]
    public void ResolvePath_WithNestedProperty_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("""{"user":{"profile":{"email":"alice@example.com"}}}""");

        var result = JsonPathEvaluator.ResolvePath(doc.RootElement, "user.profile.email");

        result.Should().Be("alice@example.com");
    }

    [Fact]
    public void ResolvePath_WithArrayBracketSyntax_ReturnsIndexedElement()
    {
        using var doc = JsonDocument.Parse("""{"items":[{"id":101},{"id":102}]}""");

        var result = JsonPathEvaluator.ResolvePath(doc.RootElement, "items[1].id");

        result.Should().Be("102");
    }

    [Fact]
    public void ResolvePath_WithNumericPartForArray_ReturnsIndexedElement()
    {
        using var doc = JsonDocument.Parse("""{"tags":["finance","urgent","internal"]}""");

        var result = JsonPathEvaluator.ResolvePath(doc.RootElement, "tags.1");

        result.Should().Be("urgent");
    }

    [Fact]
    public void ResolvePath_WhenPropertyDoesNotExist_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"name":"Alice"}""");

        var result = JsonPathEvaluator.ResolvePath(doc.RootElement, "non_existent.nested");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolvePath_WhenIndexOutOfBounds_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"items":[{"id":1}]}""");

        var result = JsonPathEvaluator.ResolvePath(doc.RootElement, "items[5].id");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolvePath_WhenEmptyOrNullPath_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"name":"Alice"}""");

        JsonPathEvaluator.ResolvePath(doc.RootElement, "").Should().BeNull();
        JsonPathEvaluator.ResolvePath(doc.RootElement, "   ").Should().BeNull();
    }

    [Fact]
    public void ResolvePath_OnArrayOfObjectsWithoutIndex_ResolvesAgainstFirstElement()
    {
        using var doc = JsonDocument.Parse("""[{"total":999.50,"currency":"THB"}]""");

        var result = JsonPathEvaluator.ResolvePath(doc.RootElement, "total");

        result.Should().Be("999.50");
    }

    [Fact]
    public void ExtractFirstScalar_WithArrayOfObjects_ReturnsFirstPropertyValue()
    {
        using var doc = JsonDocument.Parse("""[{"total":5000,"currency":"THB"},{"total":200}]""");

        var result = JsonPathEvaluator.ExtractFirstScalar(doc.RootElement);

        result.Should().Be("5000");
    }

    [Fact]
    public void ExtractFirstScalar_WithSingleObject_ReturnsFirstPropertyValue()
    {
        using var doc = JsonDocument.Parse("""{"company":"Acme Corp","id":42}""");

        var result = JsonPathEvaluator.ExtractFirstScalar(doc.RootElement);

        result.Should().Be("Acme Corp");
    }

    [Fact]
    public void ExtractFirstScalar_WithPrimitiveValue_ReturnsStringRepresentation()
    {
        using var doc = JsonDocument.Parse(""" "DirectText" """);

        var result = JsonPathEvaluator.ExtractFirstScalar(doc.RootElement);

        result.Should().Be("DirectText");
    }
}

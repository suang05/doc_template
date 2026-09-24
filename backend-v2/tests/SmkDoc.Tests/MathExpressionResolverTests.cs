using FluentAssertions;
using SmkDoc.Application.Common.Helpers;
using Xunit;

namespace SmkDoc.Tests;

public class MathExpressionResolverTests
{
    private static readonly MathExpressionResolverService Sut = new();

    private static Dictionary<string, string?> Vars(params (string k, string v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => (string?)p.v);

    [Fact]
    public void Resolve_SimpleMultiplication_ReturnsProduct()
    {
        var result = Sut.Resolve("qty * unitPrice", Vars(("qty", "3"), ("unitPrice", "250")));
        result.Should().Be("750");
    }

    [Fact]
    public void Resolve_Addition_ReturnsSum()
    {
        var result = Sut.Resolve("subtotal + vat", Vars(("subtotal", "1000"), ("vat", "70")));
        result.Should().Be("1070");
    }

    [Fact]
    public void Resolve_Subtraction_ReturnsDifference()
    {
        var result = Sut.Resolve("total - discount", Vars(("total", "500"), ("discount", "50")));
        result.Should().Be("450");
    }

    [Fact]
    public void Resolve_Division_ReturnsQuotient()
    {
        var result = Sut.Resolve("total / count", Vars(("total", "300"), ("count", "4")));
        result.Should().Be("75");
    }

    [Fact]
    public void Resolve_DecimalResult_TrimsTrailingZeros()
    {
        // 10 / 3 → 3.3333333333 (G10 trim, not 3.33333333330)
        var result = Sut.Resolve("10 / 4", Vars());
        result.Should().Be("2.5");
    }

    [Fact]
    public void Resolve_Parentheses_RespectsOperatorPrecedence()
    {
        // (2 + 3) * 4 = 20, not 2 + 12
        var result = Sut.Resolve("(base + extra) * rate",
            Vars(("base", "2"), ("extra", "3"), ("rate", "4")));
        result.Should().Be("20");
    }

    [Fact]
    public void Resolve_UnknownVariable_LeavesTokenIntact_AndReturnsEmpty()
    {
        // unknownVar stays as token → DataTable.Compute fails → returns empty
        var result = Sut.Resolve("qty * unknownVar", Vars(("qty", "5")));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_NonNumericVariable_IgnoresSubstitution()
    {
        // "hello" is not numeric → left as identifier → DataTable.Compute fails → returns empty
        var result = Sut.Resolve("qty * name", Vars(("qty", "5"), ("name", "hello")));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_EmptyExpression_ReturnsEmpty()
    {
        Sut.Resolve(string.Empty, Vars()).Should().BeEmpty();
        Sut.Resolve("   ", Vars()).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_InvalidExpression_ReturnsEmpty()
    {
        var result = Sut.Resolve("qty **", Vars(("qty", "5")));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_LiteralArithmetic_NeedNoVariables()
    {
        var result = Sut.Resolve("100 + 200", Vars());
        result.Should().Be("300");
    }

    [Fact]
    public void Resolve_ChainedExpressions_SubsequentCanReferenceEarlier()
    {
        // Simulates: line1Total already resolved, line2Total already resolved
        // totalAmount = line1Total + line2Total
        var vars = Vars(("line1Total", "1500"), ("line2Total", "2500"));
        var result = Sut.Resolve("line1Total + line2Total", vars);
        result.Should().Be("4000");
    }
}

using System.Collections.Generic;
using SmkDocServer.Application.Services;
using Xunit;

namespace SmkDocServer.Tests;

public class MathExpressionResolverTests
{
    // ─── Basic arithmetic ────────────────────────────────────────────────────

    [Fact]
    public void Addition_ReturnsCorrectResult()
    {
        var vars = new Dictionary<string, string?> { ["a"] = "10", ["b"] = "5" };
        Assert.Equal("15", MathExpressionResolver.Evaluate("a + b", vars));
    }

    [Fact]
    public void Subtraction_ReturnsCorrectResult()
    {
        var vars = new Dictionary<string, string?> { ["total"] = "100", ["discount"] = "15" };
        Assert.Equal("85", MathExpressionResolver.Evaluate("total - discount", vars));
    }

    [Fact]
    public void Multiplication_ReturnsCorrectResult()
    {
        // Use case: qty × unitPrice
        var vars = new Dictionary<string, string?> { ["qty"] = "3", ["unitPrice"] = "250.50" };
        string result = MathExpressionResolver.Evaluate("qty * unitPrice", vars);
        Assert.Equal("751.5", result);
    }

    [Fact]
    public void Division_ReturnsCorrectResult()
    {
        var vars = new Dictionary<string, string?> { ["total"] = "100", ["n"] = "4" };
        Assert.Equal("25", MathExpressionResolver.Evaluate("total / n", vars));
    }

    // ─── Invoice scenario ────────────────────────────────────────────────────

    [Fact]
    public void InvoiceSubtotal_MultipleItems()
    {
        // subtotal = qty1*price1 + qty2*price2
        var vars = new Dictionary<string, string?>
        {
            ["qty1"] = "2",
            ["price1"] = "500",
            ["qty2"] = "5",
            ["price2"] = "120"
        };
        string result = MathExpressionResolver.Evaluate("qty1 * price1 + qty2 * price2", vars);
        Assert.Equal("1600", result);
    }

    [Fact]
    public void Vat7Percent_CalculatedCorrectly()
    {
        // VAT = subtotal * 0.07
        var vars = new Dictionary<string, string?> { ["subtotal"] = "1000" };
        string result = MathExpressionResolver.Evaluate("subtotal * 0.07", vars);
        Assert.Equal("70", result);
    }

    [Fact]
    public void GrandTotal_SubtotalPlusVat()
    {
        var vars = new Dictionary<string, string?>
        {
            ["subtotal"] = "1000",
            ["vat"] = "70"
        };
        string result = MathExpressionResolver.Evaluate("subtotal + vat", vars);
        Assert.Equal("1070", result);
    }

    // ─── Function limitation ─────────────────────────────────────────────────
    // DataTable.Compute supports ONLY arithmetic operators (+, -, *, /, parentheses, IIF).
    // Math functions (ROUND, ABS, SQRT, etc.) are NOT supported.

    [Fact]
    public void Round_NotSupported_GracefulFallback()
    {
        var vars = new Dictionary<string, string?> { ["val"] = "3" };
        string result = MathExpressionResolver.Evaluate("ROUND(val / 7, 2)", vars);
        // Must not throw — returns expression text as graceful fallback
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void IIF_ConditionalExpression()
    {
        // IIF is supported by DataTable.Compute
        var vars = new Dictionary<string, string?> { ["qty"] = "10" };
        string result = MathExpressionResolver.Evaluate("IIF(qty > 5, 100, 0)", vars);
        Assert.Equal("100", result);
    }

    // ─── No-variable expressions ─────────────────────────────────────────────

    [Fact]
    public void LiteralExpression_NoVars()
    {
        var result = MathExpressionResolver.Evaluate("10 + 20 * 3", new Dictionary<string, string?>());
        Assert.Equal("70", result);
    }

    // ─── Non-numeric variable — skip substitution ────────────────────────────

    [Fact]
    public void NonNumericVariable_SkippedAndExpressionReturnedAsIs()
    {
        // "name" is a string — should not be substituted, DataTable.Compute will fail → returns expression
        var vars = new Dictionary<string, string?> { ["name"] = "สมชาย", ["qty"] = "5" };
        string result = MathExpressionResolver.Evaluate("qty * 100", vars);
        Assert.Equal("500", result); // name is skipped; qty is numeric
    }

    [Fact]
    public void NullVariable_Skipped()
    {
        var vars = new Dictionary<string, string?> { ["qty"] = null, ["price"] = "100" };
        // qty is null → skipped; expr becomes "qty * 100" with literal "qty" → Compute fails → returns expr as-is
        string result = MathExpressionResolver.Evaluate("qty * price", vars);
        // Should not throw; graceful fallback
        Assert.NotNull(result);
    }

    // ─── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public void EmptyVariables_LiteralOnly()
    {
        string result = MathExpressionResolver.Evaluate("100 + 50", new Dictionary<string, string?>());
        Assert.Equal("150", result);
    }

    [Fact]
    public void ZeroDivision_GracefulFallback()
    {
        var vars = new Dictionary<string, string?> { ["x"] = "10", ["y"] = "0" };
        // DataTable.Compute throws on /0 → fallback returns expression text
        string result = MathExpressionResolver.Evaluate("x / y", vars);
        Assert.NotNull(result); // must not throw
    }

    [Fact]
    public void DecimalInput_InvariantCulture()
    {
        // Thai locale uses "," as decimal — but our stored values use "."
        var vars = new Dictionary<string, string?> { ["price"] = "1234.56", ["qty"] = "2" };
        string result = MathExpressionResolver.Evaluate("price * qty", vars);
        Assert.Equal("2469.12", result);
    }

    [Fact]
    public void Precedence_MultiplyBeforeAdd()
    {
        var vars = new Dictionary<string, string?> { ["a"] = "2", ["b"] = "3", ["c"] = "4" };
        // 2 + 3 * 4 = 14 (not 20)
        string result = MathExpressionResolver.Evaluate("a + b * c", vars);
        Assert.Equal("14", result);
    }
}

using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Layout;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects.Layout;

public class LayoutStructTests
{
    // ── PageDimension Tests ───────────────────────────────────────────────

    [Fact]
    public void PageDimension_StandardA4_HasCorrectDimensionsAndOrientation()
    {
        var a4 = PageDimension.A4;

        a4.WidthMm.Should().Be(210f);
        a4.HeightMm.Should().Be(297f);
        a4.IsPortrait.Should().BeTrue();
        a4.IsLandscape.Should().BeFalse();
    }

    [Fact]
    public void PageDimension_SwapOrientation_SwapsWidthAndHeight()
    {
        var a4 = PageDimension.A4;
        var landscape = a4.ToLandscape();

        landscape.WidthMm.Should().Be(297f);
        landscape.HeightMm.Should().Be(210f);
        landscape.IsLandscape.Should().BeTrue();
        landscape.IsPortrait.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 297)]
    [InlineData(-10, 297)]
    [InlineData(210, 0)]
    [InlineData(210, -5)]
    [InlineData(float.NaN, 297)]
    public void PageDimension_InvalidDimensions_ThrowsDomainValidationException(float width, float height)
    {
        Action act = () => new PageDimension(width, height);
        act.Should().Throw<DomainValidationException>();
    }

    // ── PageMargin Tests ──────────────────────────────────────────────────

    [Fact]
    public void PageMargin_NormalAndUniform_HaveCorrectValues()
    {
        var normal = PageMargin.Normal;
        normal.TopMm.Should().Be(20f);
        normal.BottomMm.Should().Be(20f);
        normal.LeftMm.Should().Be(20f);
        normal.RightMm.Should().Be(20f);

        var uniform = PageMargin.Uniform(15f);
        uniform.TopMm.Should().Be(15f);
        uniform.RightMm.Should().Be(15f);
    }

    [Theory]
    [InlineData(-1, 10, 10, 10)]
    [InlineData(10, -5, 10, 10)]
    [InlineData(10, 10, -2, 10)]
    [InlineData(10, 10, 10, -0.5f)]
    public void PageMargin_NegativeMargin_ThrowsDomainValidationException(float top, float bottom, float left, float right)
    {
        Action act = () => new PageMargin(top, bottom, left, right);
        act.Should().Throw<DomainValidationException>();
    }

    // ── DateRange Tests ───────────────────────────────────────────────────

    [Fact]
    public void DateRange_ValidRange_ComputesDaysAndContainsCorrectly()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 10);
        var range = new DateRange(start, end);

        range.TotalDays.Should().Be(10);
        range.Contains(new DateOnly(2026, 1, 5)).Should().BeTrue();
        range.Contains(start).Should().BeTrue();
        range.Contains(end).Should().BeTrue();
        range.Contains(new DateOnly(2025, 12, 31)).Should().BeFalse();
        range.Contains(new DateOnly(2026, 1, 11)).Should().BeFalse();
    }

    [Fact]
    public void DateRange_StartAfterEnd_ThrowsDomainValidationException()
    {
        var start = new DateOnly(2026, 1, 15);
        var end = new DateOnly(2026, 1, 10);

        Action act = () => new DateRange(start, end);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*cannot be after end*");
    }
}

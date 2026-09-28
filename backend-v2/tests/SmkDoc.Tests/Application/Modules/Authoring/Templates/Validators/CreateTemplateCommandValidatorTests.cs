using FluentAssertions;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Validators;

public class CreateTemplateCommandValidatorTests
{
    private readonly CreateTemplateCommandValidator _validator = new();

    [Theory]
    [InlineData("valid-slug")]
    [InlineData("tax-invoice-v1")]
    [InlineData("receipt")]
    [InlineData("123-report")]
    public void Validate_ValidSlug_ShouldPass(string slug)
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Valid Name", slug, "Category");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("INVALID_UPPERCASE")]
    [InlineData("slug with spaces")]
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphens")]
    [InlineData("")]
    public void Validate_InvalidSlug_ShouldFail(string slug)
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), "Valid Name", slug, "Category");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Slug");
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Validate_InvalidName_ShouldFail(string name)
    {
        var command = new CreateTemplateCommand(Guid.NewGuid(), name, "valid-slug", "Category");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_EmptyProjectId_ShouldFail()
    {
        var command = new CreateTemplateCommand(Guid.Empty, "Valid Name", "valid-slug", "Category");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProjectId");
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmkDoc.Api.Binders;
using SmkDoc.Api.OpenApi;

namespace SmkDoc.Tests.Api.Binders;

public sealed class IfMatchVersionModelBinderTests
{
    private readonly IfMatchVersionModelBinder _sut = new();

    private static DefaultModelBindingContext CreateBindingContext(string? ifMatchHeaderValue)
    {
        var httpContext = new DefaultHttpContext();
        if (ifMatchHeaderValue is not null)
        {
            httpContext.Request.Headers[OpenApiNames.IfMatchHeader] = ifMatchHeaderValue;
        }

        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(long));
        return new DefaultModelBindingContext
        {
            ActionContext = new ActionContext { HttpContext = httpContext },
            ModelMetadata = metadata,
            ModelName = "expectedVersion",
            FieldName = OpenApiNames.IfMatchHeader,
            BindingSource = BindingSource.Header,
            ModelState = new ModelStateDictionary()
        };
    }

    [Theory]
    [InlineData("\"v1\"", 1L)]
    [InlineData("\"v42\"", 42L)]
    [InlineData("  \"v9999\"  ", 9999L)]
    public async Task BindModelAsync_WhenStrongVersionETagProvided_BindsVersionSuccessfully(
        string headerValue,
        long expectedVersion)
    {
        var bindingContext = CreateBindingContext(headerValue);

        await _sut.BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BindModelAsync_WhenIfMatchHeaderMissingOrBlank_ThrowsPreconditionRequired(string? headerValue)
    {
        var bindingContext = CreateBindingContext(headerValue);

        var act = () => _sut.BindModelAsync(bindingContext);

        var ex = await act.Should().ThrowAsync<PreconditionFailedException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.PreconditionRequired);
    }

    [Theory]
    [InlineData("W/\"v1\"")]
    [InlineData("*")]
    [InlineData("v1")]
    [InlineData("\"1\"")]
    [InlineData("\"v0\"")]
    [InlineData("\"v-1\"")]
    [InlineData("\"vabc\"")]
    [InlineData("\"v\"")]
    public async Task BindModelAsync_WhenWeakWildcardOrMalformedETag_ThrowsPreconditionFailed(string headerValue)
    {
        var bindingContext = CreateBindingContext(headerValue);

        var act = () => _sut.BindModelAsync(bindingContext);

        var ex = await act.Should().ThrowAsync<PreconditionFailedException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.PreconditionFailed);
    }

    [Fact]
    public void FromIfMatchAttribute_ConfiguresHeaderBindingAndModelBinderType()
    {
        var attribute = new FromIfMatchAttribute();

        attribute.BinderType.Should().Be<IfMatchVersionModelBinder>();
        attribute.BindingSource.Should().Be(BindingSource.Header);
        attribute.Name.Should().Be(OpenApiNames.IfMatchHeader);
    }
}

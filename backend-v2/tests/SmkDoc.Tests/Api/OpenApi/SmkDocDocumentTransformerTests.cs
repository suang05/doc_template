using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;
using SmkDoc.Api.OpenApi;
using SmkDoc.Api.OpenApi.Transformers;

namespace SmkDoc.Tests.Api.OpenApi;

public sealed class SmkDocDocumentTransformerTests
{
    private static SmkDocDocumentTransformer CreateSut(string? serverUrl = null)
    {
        var values = new Dictionary<string, string?>();
        if (serverUrl is not null)
        {
            values[OpenApiNames.ServerUrlConfigKey] = serverUrl;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new SmkDocDocumentTransformer(configuration);
    }

    private static OpenApiDocumentTransformerContext CreateContext() => new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = Substitute.For<IServiceProvider>()
    };

    private static async Task<OpenApiDocument> TransformAsync(SmkDocDocumentTransformer sut)
    {
        var document = new OpenApiDocument();
        await sut.TransformAsync(document, CreateContext(), CancellationToken.None);
        return document;
    }

    [Fact]
    public async Task TransformAsync_WhenNoServerConfigured_ShouldUseRelativeRoot()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        var document = await TransformAsync(sut);

        // Step 3: Assert
        document.Servers.Should().ContainSingle().Which.Url.Should().Be("/");
    }

    [Fact]
    public async Task TransformAsync_WhenServerConfigured_ShouldUseConfiguredUrl()
    {
        // Step 1: Arrange
        var sut = CreateSut("https://api.example.com");

        // Step 2: Act
        var document = await TransformAsync(sut);

        // Step 3: Assert
        document.Servers.Should().ContainSingle().Which.Url.Should().Be("https://api.example.com");
    }

    [Fact]
    public async Task TransformAsync_WhenExecuted_ShouldSetSemanticVersion()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        var document = await TransformAsync(sut);

        // Step 3: Assert
        document.Info.Version.Should().Be(OpenApiNames.ApiVersion);
    }

    [Fact]
    public async Task TransformAsync_WhenExecuted_ShouldDeclareBothSecuritySchemes()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        var document = await TransformAsync(sut);

        // Step 3: Assert
        document.Components!.SecuritySchemes.Should().ContainKeys(OpenApiNames.ApiKeyScheme, OpenApiNames.BearerScheme);
    }

    [Fact]
    public async Task TransformAsync_WhenExecuted_ShouldConfigureGlobalSecurityAsApiKeyOrBearer()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        var document = await TransformAsync(sut);

        // Step 3: Assert — two separate requirement objects mean OR
        document.Security.Should().HaveCount(2);
        document.Security!.All(requirement => requirement.Count == 1).Should().BeTrue();
    }

    [Fact]
    public async Task TransformAsync_WhenExecuted_ShouldRegisterProblemDetailsSchemaWithOneOfFieldErrors()
    {
        // Step 1: Arrange
        var sut = CreateSut();

        // Step 2: Act
        var document = await TransformAsync(sut);

        // Step 3: Assert
        var schema = document.Components!.Schemas![OpenApiNames.ProblemDetailsSchema];
        schema.Properties.Should().ContainKeys("type", "title", "status", "detail", "instance", "errorCode", "errors");
        schema.Properties!["errors"].OneOf.Should().HaveCount(2);
    }
}

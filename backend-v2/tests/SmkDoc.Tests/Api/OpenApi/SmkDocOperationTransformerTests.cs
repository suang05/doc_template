using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SmkDoc.Api.Filters;
using SmkDoc.Api.OpenApi;
using SmkDoc.Api.OpenApi.Transformers;

namespace SmkDoc.Tests.Api.OpenApi;

public sealed class SmkDocOperationTransformerTests
{
    private static SmkDocOperationTransformer CreateSut() => new();

    private static OpenApiOperationTransformerContext CreateContext(
        string relativePath,
        string controller = "DocumentGeneration",
        string action = "Generate",
        params object[] metadata)
    {
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = controller,
            ActionName = action,
            EndpointMetadata = metadata
        };
        descriptor.RouteValues["controller"] = controller;
        descriptor.RouteValues["action"] = action;

        var description = new ApiDescription
        {
            RelativePath = relativePath,
            ActionDescriptor = descriptor
        };

        return new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = description,
            ApplicationServices = Substitute.For<IServiceProvider>()
        };
    }

    private static OpenApiOperation CreateOperation() => new()
    {
        Responses = new OpenApiResponses
        {
            ["200"] = new OpenApiResponse { Description = "OK" },
            ["400"] = new OpenApiResponse
            {
                Description = "Bad Request",
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() }
            }
        }
    };

    [Fact]
    public async Task TransformAsync_SetsOperationIdFromControllerAndAction()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/documents/generate/{slug}");

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.OperationId.Should().Be("DocumentGeneration_Generate");
    }

    [Fact]
    public async Task TransformAsync_PrefersRealMethodNameSoAsyncOverloadsStayUnique()
    {
        // Step 1: Arrange — MVC strips "Async" from the routed action name; the MethodInfo keeps it
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/documents/generate-async/{slug}", action: "Generate");
        ((ControllerActionDescriptor)context.Description.ActionDescriptor).MethodInfo =
            typeof(AsyncSample).GetMethod(nameof(AsyncSample.GenerateAsync))!;

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.OperationId.Should().Be("DocumentGeneration_GenerateAsync");
    }

    private static class AsyncSample
    {
        public static Task GenerateAsync() => Task.CompletedTask;
    }

    [Theory]
    [InlineData("api/v1/auth/login")]
    [InlineData("api/v1/auth/refresh")]
    [InlineData("api/v1/schemas/validate")]
    public async Task TransformAsync_PublicRoute_OverridesSecurityWithEmptyList(string relativePath)
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext(relativePath);

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.Security.Should().NotBeNull().And.BeEmpty();
        operation.Responses!.Keys.Should().NotContain(["401", "403"]);
        operation.Responses.Keys.Should().Contain(["429", "500"]);
    }

    [Fact]
    public async Task TransformAsync_ProtectedRoute_KeepsGlobalSecurityAndAddsAuthErrors()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/templates");

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.Security.Should().BeNull();
        operation.Responses!.Keys.Should().Contain(["401", "403", "429", "500"]);
    }

    [Fact]
    public async Task TransformAsync_ErrorResponses_UseProblemJsonWithProblemDetailsSchema()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/templates");

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        foreach (var statusCode in new[] { "400", "401", "403", "429", "500" })
        {
            var content = operation.Responses![statusCode].Content;
            content.Should().NotBeNull();
            content!.Keys.Should().ContainSingle().Which.Should().Be(OpenApiNames.ProblemJsonMediaType);
        }

        operation.Responses!["200"].Content.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task TransformAsync_ExistingErrorDescription_IsPreserved()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/templates");

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.Responses!["400"].Description.Should().Be("Bad Request");
    }

    [Fact]
    public async Task TransformAsync_IdempotentAction_AddsOptionalIdempotencyKeyHeader()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/documents/generate/{slug}", metadata: new IdempotentAttribute());

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        var header = operation.Parameters.Should().ContainSingle().Subject;
        header.Name.Should().Be(OpenApiNames.IdempotencyKeyHeader);
        header.In.Should().Be(ParameterLocation.Header);
        header.Required.Should().BeFalse();
    }

    [Fact]
    public async Task TransformAsync_MandatoryIdempotentAction_MarksHeaderRequired()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/documents/generate/{slug}", metadata: new IdempotentAttribute { Mandatory = true });

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.Parameters.Should().ContainSingle().Which.Required.Should().BeTrue();
    }

    [Fact]
    public async Task TransformAsync_NonIdempotentAction_AddsNoHeader()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var operation = CreateOperation();
        var context = CreateContext("api/v1/templates");

        // Step 2: Act
        await sut.TransformAsync(operation, context, CancellationToken.None);

        // Step 3: Assert
        operation.Parameters.Should().BeNullOrEmpty();
    }
}

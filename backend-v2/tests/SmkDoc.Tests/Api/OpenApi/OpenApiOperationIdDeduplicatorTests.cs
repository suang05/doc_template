using Microsoft.OpenApi;
using SmkDoc.Api.OpenApi;

namespace SmkDoc.Tests.Api.OpenApi;

public sealed class OpenApiOperationIdDeduplicatorTests
{
    private static OpenApiDocument CreateDocument(params (string Path, string OperationId)[] getOperations)
    {
        var paths = new OpenApiPaths();
        foreach (var (path, operationId) in getOperations)
        {
            paths[path] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation { OperationId = operationId }
                }
            };
        }

        return new OpenApiDocument { Paths = paths };
    }

    private static string IdOf(OpenApiDocument document, string path) =>
        document.Paths[path].Operations![HttpMethod.Get].OperationId!;

    [Fact]
    public void Apply_UniqueIds_AreLeftUntouched()
    {
        // Step 1: Arrange
        var document = CreateDocument(("/api/v1/a", "A_One"), ("/api/v1/b", "B_Two"));

        // Step 2: Act
        OpenApiOperationIdDeduplicator.Apply(document);

        // Step 3: Assert
        IdOf(document, "/api/v1/a").Should().Be("A_One");
        IdOf(document, "/api/v1/b").Should().Be("B_Two");
    }

    [Fact]
    public void Apply_AliasRoutes_ShortestPathKeepsBaseIdAndAliasGetsPathSuffix()
    {
        // Step 1: Arrange
        var document = CreateDocument(
            ("/api/v1/management/settings/fonts", "FontManagement_ListFonts"),
            ("/api/v1/fonts", "FontManagement_ListFonts"));

        // Step 2: Act
        OpenApiOperationIdDeduplicator.Apply(document);

        // Step 3: Assert
        IdOf(document, "/api/v1/fonts").Should().Be("FontManagement_ListFonts");
        IdOf(document, "/api/v1/management/settings/fonts").Should().Be("FontManagement_ListFonts_ManagementSettingsFonts");
    }

    [Fact]
    public void Apply_AfterRenaming_AllIdsAreUnique()
    {
        // Step 1: Arrange
        var document = CreateDocument(
            ("/api/v1/templates/{slug}/validate", "TemplateAsset_ValidatePayload"),
            ("/api/v1/templates/{slug}/validate-payload", "TemplateAsset_ValidatePayload"),
            ("/api/v1/other", "Other_Get"));

        // Step 2: Act
        OpenApiOperationIdDeduplicator.Apply(document);

        // Step 3: Assert
        var ids = document.Paths.Values.SelectMany(item => item.Operations!.Values).Select(op => op.OperationId).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("/api/v1/management/settings/fonts", "ManagementSettingsFonts")]
    [InlineData("/api/v1/templates/{id}/studio/bundle", "TemplatesIdStudioBundle")]
    [InlineData("/api/v1/templates/{slug}/validate-payload", "TemplatesSlugValidatePayload")]
    public void BuildPathSuffix_StripsApiPrefixAndPascalCasesSegments(string path, string expected)
    {
        // Step 1 & 2: Act
        var result = OpenApiOperationIdDeduplicator.BuildPathSuffix(path);

        // Step 3: Assert
        result.Should().Be(expected);
    }
}


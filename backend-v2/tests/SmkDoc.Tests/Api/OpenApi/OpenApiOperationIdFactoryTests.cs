using SmkDoc.Api.OpenApi;

namespace SmkDoc.Tests.Api.OpenApi;

public sealed class OpenApiOperationIdFactoryTests
{
    [Fact]
    public void Create_WhenValidControllerAndAction_ShouldComposeOperationId()
    {
        // Step 1: Arrange
        const string controller = "DocumentGeneration";
        const string action = "Generate";

        // Step 2: Act
        var result = OpenApiOperationIdFactory.Create(controller, action);

        // Step 3: Assert
        result.Should().Be("DocumentGeneration_Generate");
    }

    [Fact]
    public void Create_WhenInputsContainSurroundingWhitespace_ShouldTrimSegments()
    {
        // Step 1: Arrange
        const string controller = " DocumentGeneration ";

        // Step 2: Act
        var result = OpenApiOperationIdFactory.Create(controller, " Generate ");

        // Step 3: Assert
        result.Should().Be("DocumentGeneration_Generate");
    }

    [Theory]
    [InlineData("", "Action")]
    [InlineData("Controller", " ")]
    public void Create_WhenSegmentIsBlank_ShouldThrowArgumentException(string controller, string action)
    {
        // Step 1: Arrange
        var act = () => OpenApiOperationIdFactory.Create(controller, action);

        // Step 2 & 3: Act + Assert
        act.Should().Throw<ArgumentException>();
    }
}

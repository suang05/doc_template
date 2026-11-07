namespace SmkDoc.Api.OpenApi;

/// <summary>
/// Builds stable, unique <c>operationId</c> values of the form <c>{Controller}_{Action}</c>.
/// Client generators use this value as the function name, so it must not depend on route shape.
/// </summary>
public static class OpenApiOperationIdFactory
{
    public static string Create(string controller, string action)
    {
        // Step 1: Parse & Guard
        ArgumentException.ThrowIfNullOrWhiteSpace(controller);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        // Step 2: Compose
        return $"{controller.Trim()}_{action.Trim()}";
    }
}


using FluentValidation;
using FluentValidation.Results;

namespace SmkDoc.Application.Common.Behaviors;

/// <summary>
/// Pipeline decorator executing all registered <see cref="IValidator{TRequest}"/> instances at Line 1
/// before delegating to the inner <see cref="IUseCase{TRequest, TResponse}"/>.
/// </summary>
public sealed class ValidatingUseCaseDecorator<TRequest, TResponse>(
    IUseCase<TRequest, TResponse> innerUseCase,
    IEnumerable<IValidator<TRequest>> validators) : IUseCase<TRequest, TResponse>
{
    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct)
    {
        await ValidationPipelineExecutor.ValidateAndThrowAsync(request, validators, ct);
        return await innerUseCase.ExecuteAsync(request, ct);
    }
}

/// <summary>
/// Pipeline decorator executing all registered <see cref="IValidator{TRequest}"/> instances at Line 1
/// before delegating to the inner void-returning <see cref="IUseCase{TRequest}"/>.
/// </summary>
public sealed class ValidatingUseCaseDecorator<TRequest>(
    IUseCase<TRequest> innerUseCase,
    IEnumerable<IValidator<TRequest>> validators) : IUseCase<TRequest>
{
    public async Task ExecuteAsync(TRequest request, CancellationToken ct)
    {
        await ValidationPipelineExecutor.ValidateAndThrowAsync(request, validators, ct);
        await innerUseCase.ExecuteAsync(request, ct);
    }
}

internal static class ValidationPipelineExecutor
{
    public static async Task ValidateAndThrowAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken ct)
    {
        if (request is null)
        {
            throw new ValidationException(
                new Dictionary<string, string[]>
                {
                    ["request"] = ["Request payload cannot be null."]
                },
                [new ValidationErrorEntry("/request", "NOT_NULL", "Request payload cannot be null.")]);
        }

        if (validators is ICollection<IValidator<TRequest>> { Count: 0 })
        {
            return;
        }

        List<ValidationFailure>? failures = null;
        ValidationContext<TRequest>? context = null;

        foreach (var validator in validators)
        {
            context ??= new ValidationContext<TRequest>(request);
            var validationResult = await validator.ValidateAsync(context, ct);
            if (!validationResult.IsValid)
            {
                failures ??= [];
                failures.AddRange(validationResult.Errors);
            }
        }

        if (failures is { Count: > 0 })
        {
            var errors = failures
                .GroupBy(failure => NormalizePropertyPathToCamelCase(failure.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

            var errorEntries = failures
                .Select(failure => new ValidationErrorEntry(
                    ValidationErrorEntry.ToJsonPointer(failure.PropertyName, camelCaseSegments: true),
                    ValidationErrorEntry.NormalizeErrorCode(failure.ErrorCode),
                    failure.ErrorMessage))
                .Distinct()
                .ToArray();

            throw new ValidationException(errors, errorEntries);
        }
    }

    internal static string NormalizePropertyPathToCamelCase(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
        {
            return string.Empty;
        }

        if (!propertyPath.Contains('.'))
        {
            return ToCamelCaseSegment(propertyPath);
        }

        var segments = propertyPath.Split('.');
        for (int index = 0; index < segments.Length; index++)
        {
            segments[index] = ToCamelCaseSegment(segments[index]);
        }

        return string.Join('.', segments);
    }

    private static string ToCamelCaseSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || !char.IsUpper(segment[0]))
        {
            return segment;
        }

        if (segment.Length == 1)
        {
            return char.ToLowerInvariant(segment[0]).ToString();
        }

        return string.Concat(char.ToLowerInvariant(segment[0]).ToString(), segment.AsSpan(1));
    }
}

using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using DomainValidationException = SmkDoc.Domain.Exceptions.ValidationException;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Automatic action filter that inspects controller action arguments and executes
/// registered FluentValidation <see cref="IValidator{T}"/> if one is present in DI.
/// If validation fails, throws a <see cref="ValidationException"/> that is converted
/// to an RFC 7807 400 Bad Request by <see cref="GlobalExceptionFilter"/>.
/// </summary>
public sealed class ValidateCommandFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var (_, argument) in context.ActionArguments)
        {
            if (argument is null)
            {
                continue;
            }

            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = serviceProvider.GetService(validatorType) as IValidator;

            if (validator is not null)
            {
                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .GroupBy(e => ToCamelCase(e.PropertyName))
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()
                        );

                    throw new DomainValidationException(errors);
                }
            }
        }

        await next();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length == 1) return char.ToLowerInvariant(name[0]).ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

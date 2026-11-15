using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Application.Common.Decorators;

namespace SmkDoc.Application.Common.Behaviors;

/// <summary>
/// Registers Use Cases wrapped in the canonical Application execution pipeline:
/// <c>LoggingUseCaseDecorator -> [IdempotencyDecorator] -> ValidatingUseCaseDecorator -> UseCase</c>.
/// </summary>
public static class UseCaseServiceCollectionExtensions
{
    public static IServiceCollection AddUseCase<TRequest, TResponse, TImplementation>(this IServiceCollection services)
        where TImplementation : class, IUseCase<TRequest, TResponse>
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<IUseCase<TRequest, TResponse>>(serviceProvider =>
        {
            IUseCase<TRequest, TResponse> pipeline = new ValidatingUseCaseDecorator<TRequest, TResponse>(
                serviceProvider.GetRequiredService<TImplementation>(),
                serviceProvider.GetServices<IValidator<TRequest>>());

            if (typeof(IIdempotentCommand).IsAssignableFrom(typeof(TRequest)))
            {
                pipeline = new IdempotencyDecorator<TRequest, TResponse>(
                    pipeline,
                    serviceProvider.GetRequiredService<IIdempotencyStore>(),
                    serviceProvider.GetRequiredService<IIdempotencyExecutionScope>(),
                    serviceProvider.GetRequiredService<ICurrentProjectContext>());
            }

            var logger = serviceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger<LoggingUseCaseDecorator<TRequest, TResponse>>()
                ?? NullLogger<LoggingUseCaseDecorator<TRequest, TResponse>>.Instance;
            var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

            return new LoggingUseCaseDecorator<TRequest, TResponse>(pipeline, logger, timeProvider);
        });
        return services;
    }

    public static IServiceCollection AddUseCase<TRequest, TImplementation>(this IServiceCollection services)
        where TImplementation : class, IUseCase<TRequest>
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<IUseCase<TRequest>>(serviceProvider =>
        {
            IUseCase<TRequest> pipeline = new ValidatingUseCaseDecorator<TRequest>(
                serviceProvider.GetRequiredService<TImplementation>(),
                serviceProvider.GetServices<IValidator<TRequest>>());

            if (typeof(IIdempotentCommand).IsAssignableFrom(typeof(TRequest)))
            {
                pipeline = new IdempotencyDecorator<TRequest>(
                    pipeline,
                    serviceProvider.GetRequiredService<IIdempotencyStore>(),
                    serviceProvider.GetRequiredService<IIdempotencyExecutionScope>(),
                    serviceProvider.GetRequiredService<ICurrentProjectContext>());
            }

            var logger = serviceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger<LoggingUseCaseDecorator<TRequest>>()
                ?? NullLogger<LoggingUseCaseDecorator<TRequest>>.Instance;
            var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

            return new LoggingUseCaseDecorator<TRequest>(pipeline, logger, timeProvider);
        });
        return services;
    }
}

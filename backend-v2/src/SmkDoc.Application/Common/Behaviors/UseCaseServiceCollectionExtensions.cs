using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SmkDoc.Application.Common.Behaviors;

/// <summary>
/// AOT-safe, zero-reflection dependency injection extensions registering UseCases wrapped in
/// <see cref="ValidatingUseCaseDecorator{TRequest, TResponse}"/>.
/// </summary>
public static class UseCaseServiceCollectionExtensions
{
    public static IServiceCollection AddUseCase<TRequest, TResponse, TImplementation>(this IServiceCollection services)
        where TImplementation : class, IUseCase<TRequest, TResponse>
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<IUseCase<TRequest, TResponse>>(serviceProvider =>
            new ValidatingUseCaseDecorator<TRequest, TResponse>(
                serviceProvider.GetRequiredService<TImplementation>(),
                serviceProvider.GetServices<IValidator<TRequest>>()));
        return services;
    }

    public static IServiceCollection AddUseCase<TRequest, TImplementation>(this IServiceCollection services)
        where TImplementation : class, IUseCase<TRequest>
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<IUseCase<TRequest>>(serviceProvider =>
            new ValidatingUseCaseDecorator<TRequest>(
                serviceProvider.GetRequiredService<TImplementation>(),
                serviceProvider.GetServices<IValidator<TRequest>>()));
        return services;
    }
}

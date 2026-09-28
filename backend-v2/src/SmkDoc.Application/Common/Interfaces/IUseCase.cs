namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Defines a single-responsibility Use Case with an input request and an output response.
/// </summary>
/// <typeparam name="TRequest">The input command or query.</typeparam>
/// <typeparam name="TResponse">The output DTO or result.</typeparam>
public interface IUseCase<in TRequest, TResponse>
{
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct = default);
}

/// <summary>
/// Defines a single-responsibility Use Case with an input request and no return value.
/// </summary>
/// <typeparam name="TRequest">The input command or request.</typeparam>
public interface IUseCase<in TRequest>
{
    Task ExecuteAsync(TRequest request, CancellationToken ct = default);
}

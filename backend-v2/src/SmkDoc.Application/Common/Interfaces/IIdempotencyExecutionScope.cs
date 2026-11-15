namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Coordinates HTTP <c>IdempotencyFilter</c> and Application
/// <see cref="Decorators.IdempotencyDecorator{TRequest, TResponse}"/> so both layers never double-acquire the same key
/// within a single request.
/// </summary>
public interface IIdempotencyExecutionScope
{
    bool IsFilterManaged { get; }
    void MarkFilterManaged();
}

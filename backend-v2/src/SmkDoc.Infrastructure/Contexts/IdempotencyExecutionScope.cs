using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Contexts;

/// <summary>
/// Scoped request coordination flag ensuring HTTP IdempotencyFilter and Application
/// IdempotencyDecorator never double-acquire the same idempotency key within a single request.
/// </summary>
public sealed class IdempotencyExecutionScope : IIdempotencyExecutionScope
{
    public bool IsFilterManaged { get; private set; }

    public void MarkFilterManaged() => IsFilterManaged = true;
}

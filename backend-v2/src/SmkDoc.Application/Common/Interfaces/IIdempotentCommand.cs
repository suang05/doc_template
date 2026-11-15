namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Marker contract for Application commands protected by <see cref="Decorators.IdempotencyDecorator{TRequest, TResponse}"/>.
/// </summary>
public interface IIdempotentCommand
{
    string? IdempotencyKey { get; }
    string IdempotencyScope { get; }
    string? RequestFingerprint => null;
}

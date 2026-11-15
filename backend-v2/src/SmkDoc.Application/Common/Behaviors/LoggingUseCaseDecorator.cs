using Microsoft.Extensions.Logging;

namespace SmkDoc.Application.Common.Behaviors;

internal static class UseCaseLoggingMessages
{
    private static readonly Action<ILogger, string, Exception?> ExecutingMessage = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1001, nameof(Executing)),
        "Executing use case {UseCaseName}");

    private static readonly Action<ILogger, string, double, Exception?> CompletedMessage = LoggerMessage.Define<string, double>(
        LogLevel.Information,
        new EventId(1002, nameof(Completed)),
        "Completed use case {UseCaseName} in {ElapsedMs:F2} ms");

    private static readonly Action<ILogger, string, double, Exception?> FailedMessage = LoggerMessage.Define<string, double>(
        LogLevel.Warning,
        new EventId(1003, nameof(Failed)),
        "Use case {UseCaseName} terminated unsuccessfully after {ElapsedMs:F2} ms");

    internal static void Executing(ILogger logger, string useCaseName) =>
        ExecutingMessage(logger, useCaseName, null);

    internal static void Completed(ILogger logger, string useCaseName, double elapsedMs) =>
        CompletedMessage(logger, useCaseName, elapsedMs, null);

    internal static void Failed(ILogger logger, string useCaseName, double elapsedMs) =>
        FailedMessage(logger, useCaseName, elapsedMs, null);
}

/// <summary>
/// Outermost observability decorator wrapping every <see cref="IUseCase{TRequest, TResponse}"/>
/// to emit structured start, completion, and duration telemetry without logging sensitive payloads.
/// </summary>
public sealed class LoggingUseCaseDecorator<TRequest, TResponse>(
    IUseCase<TRequest, TResponse> inner,
    ILogger<LoggingUseCaseDecorator<TRequest, TResponse>> logger,
    TimeProvider timeProvider) : IUseCase<TRequest, TResponse>
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        UseCaseLoggingMessages.Executing(logger, RequestName);
        long startedAt = timeProvider.GetTimestamp();
        bool succeeded = false;

        try
        {
            var response = await inner.ExecuteAsync(request, ct).ConfigureAwait(false);
            succeeded = true;
            return response;
        }
        finally
        {
            double elapsedMs = timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
            if (succeeded)
            {
                UseCaseLoggingMessages.Completed(logger, RequestName, elapsedMs);
            }
            else
            {
                UseCaseLoggingMessages.Failed(logger, RequestName, elapsedMs);
            }
        }
    }
}

/// <summary>
/// Outermost observability decorator wrapping every <see cref="IUseCase{TRequest}"/>
/// to emit structured start, completion, and duration telemetry without logging sensitive payloads.
/// </summary>
public sealed class LoggingUseCaseDecorator<TRequest>(
    IUseCase<TRequest> inner,
    ILogger<LoggingUseCaseDecorator<TRequest>> logger,
    TimeProvider timeProvider) : IUseCase<TRequest>
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public async Task ExecuteAsync(TRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        UseCaseLoggingMessages.Executing(logger, RequestName);
        long startedAt = timeProvider.GetTimestamp();
        bool succeeded = false;

        try
        {
            await inner.ExecuteAsync(request, ct).ConfigureAwait(false);
            succeeded = true;
        }
        finally
        {
            double elapsedMs = timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
            if (succeeded)
            {
                UseCaseLoggingMessages.Completed(logger, RequestName, elapsedMs);
            }
            else
            {
                UseCaseLoggingMessages.Failed(logger, RequestName, elapsedMs);
            }
        }
    }
}

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Events;

/// <summary>
/// Dispatches domain events post-commit via cached strongly-typed wrappers (zero per-event reflection).
/// Individual handler failures are logged and isolated so post-commit side-effects never invalidate
/// already-committed database transactions or trigger compensating object storage rollbacks.
/// </summary>
public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, DomainEventHandlerWrapper> HandlerWrappers = new();

    public async Task DispatchEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            ct.ThrowIfCancellationRequested();

            var eventType = domainEvent.GetType();
            logger.LogInformation("Dispatching post-commit domain event {EventType}", eventType.Name);

            var wrapper = HandlerWrappers.GetOrAdd(eventType, static type =>
            {
                var wrapperType = typeof(DomainEventHandlerWrapperImpl<>).MakeGenericType(type);
                return (DomainEventHandlerWrapper)Activator.CreateInstance(wrapperType)!;
            });

            await wrapper.HandleAsync(domainEvent, serviceProvider, logger, ct);
        }
    }

    private abstract class DomainEventHandlerWrapper
    {
        public abstract Task HandleAsync(
            IDomainEvent domainEvent,
            IServiceProvider serviceProvider,
            ILogger logger,
            CancellationToken ct);
    }

    private sealed class DomainEventHandlerWrapperImpl<TEvent> : DomainEventHandlerWrapper
        where TEvent : IDomainEvent
    {
        public override async Task HandleAsync(
            IDomainEvent domainEvent,
            IServiceProvider serviceProvider,
            ILogger logger,
            CancellationToken ct)
        {
            var typedEvent = (TEvent)domainEvent;
            var handlers = serviceProvider.GetServices<IDomainEventHandler<TEvent>>();

            foreach (var handler in handlers)
            {
                try
                {
                    await handler.HandleAsync(typedEvent, ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "Post-commit domain event handler {HandlerType} failed while handling {EventType}; isolating fault.",
                        handler.GetType().Name,
                        typeof(TEvent).Name);
                }
            }
        }
    }
}

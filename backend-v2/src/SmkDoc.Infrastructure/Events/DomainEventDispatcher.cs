using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Events;

/// <summary>
/// Dispatches domain events to registered application-level event handlers post-commit.
/// </summary>
public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType();
            logger.LogInformation("Dispatching domain event {EventType}", eventType.Name);

            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler == null) continue;

                var method = handlerType.GetMethod(
                    nameof(IDomainEventHandler<IDomainEvent>.HandleAsync),
                    BindingFlags.Public | BindingFlags.Instance);

                if (method != null)
                {
                    var task = (Task?)method.Invoke(handler, [domainEvent, ct]);
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Events;

namespace SmkDoc.Tests.Infrastructure.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchEventsAsync_WhenHandlerRegistered_InvokesHandleAsync()
    {
        // Arrange
        var handler = Substitute.For<IDomainEventHandler<TemplatePublishedDomainEvent>>();
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        var serviceProvider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(serviceProvider, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new TemplatePublishedDomainEvent(TemplateId.New(), ProjectId.New(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);

        // Act
        await dispatcher.DispatchEventsAsync([domainEvent], CancellationToken.None);

        // Assert
        await handler.Received(1).HandleAsync(domainEvent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchEventsAsync_WhenNoHandlerRegistered_CompletesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(serviceProvider, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new TemplatePublishedDomainEvent(TemplateId.New(), ProjectId.New(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);

        // Act & Assert (Should not throw)
        await dispatcher.DispatchEventsAsync([domainEvent], CancellationToken.None);
    }

    [Fact]
    public async Task DispatchEventsAsync_WithMultipleHandlers_InvokesAllHandlers()
    {
        // Arrange
        var handler1 = Substitute.For<IDomainEventHandler<ApiKeyRevokedDomainEvent>>();
        var handler2 = Substitute.For<IDomainEventHandler<ApiKeyRevokedDomainEvent>>();

        var services = new ServiceCollection();
        services.AddSingleton(handler1);
        services.AddSingleton(handler2);
        var serviceProvider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(serviceProvider, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new ApiKeyRevokedDomainEvent(ApiKeyId.New(), ProjectId.New(), DateTimeOffset.UtcNow);

        // Act
        await dispatcher.DispatchEventsAsync([domainEvent], CancellationToken.None);

        // Assert
        await handler1.Received(1).HandleAsync(domainEvent, Arg.Any<CancellationToken>());
        await handler2.Received(1).HandleAsync(domainEvent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchEventsAsync_WhenFirstHandlerThrowsException_IsolatesFaultAndExecutesSubsequentHandlers()
    {
        // Arrange
        var failingHandler = Substitute.For<IDomainEventHandler<ApiKeyRevokedDomainEvent>>();
        failingHandler.HandleAsync(Arg.Any<ApiKeyRevokedDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Audit sink unavailable")));

        var healthyHandler = Substitute.For<IDomainEventHandler<ApiKeyRevokedDomainEvent>>();

        var services = new ServiceCollection();
        services.AddSingleton(failingHandler);
        services.AddSingleton(healthyHandler);
        var serviceProvider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(serviceProvider, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new ApiKeyRevokedDomainEvent(ApiKeyId.New(), ProjectId.New(), TestConstants.BaselineTime);

        // Act
        var act = () => dispatcher.DispatchEventsAsync([domainEvent], CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await failingHandler.Received(1).HandleAsync(domainEvent, Arg.Any<CancellationToken>());
        await healthyHandler.Received(1).HandleAsync(domainEvent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchEventsAsync_WhenHandlerThrowsOperationCanceledException_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var canceledHandler = Substitute.For<IDomainEventHandler<ApiKeyRevokedDomainEvent>>();
        canceledHandler.HandleAsync(Arg.Any<ApiKeyRevokedDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(cts.Token));

        var services = new ServiceCollection();
        services.AddSingleton(canceledHandler);
        var serviceProvider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(serviceProvider, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new ApiKeyRevokedDomainEvent(ApiKeyId.New(), ProjectId.New(), TestConstants.BaselineTime);

        // Act
        var act = () => dispatcher.DispatchEventsAsync([domainEvent], cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

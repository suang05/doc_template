namespace SmkDoc.Infrastructure.Persistence.Outbox;

/// <summary>
/// Durable PostgreSQL transactional outbox row storing serialized domain events until dispatched by <see cref="BackgroundWorkers.OutboxRelayWorker"/>.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; init; }
    public string? LockedBy { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset? DeadLetteredAt { get; init; }
    public int Attempts { get; private set; }
    public string? LastError { get; init; }

    private OutboxMessage() { }

    private OutboxMessage(
        Guid id,
        Guid? projectId,
        string eventType,
        string payloadJson,
        DateTimeOffset occurredAt,
        DateTimeOffset availableAt,
        int attempts)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ProjectId = projectId;
        EventType = eventType;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        AvailableAt = availableAt;
        Attempts = attempts;
    }

    public static OutboxMessage Create(
        Guid id,
        Guid? projectId,
        string eventType,
        string payloadJson,
        DateTimeOffset occurredAt) =>
        new(id, projectId, eventType, payloadJson, occurredAt, occurredAt, 0);

    public static OutboxMessage RehydrateClaimed(
        Guid id,
        Guid? projectId,
        string eventType,
        string payloadJson,
        DateTimeOffset occurredAt,
        int attempts) =>
        new(id, projectId, eventType, payloadJson, occurredAt, occurredAt, attempts);
}

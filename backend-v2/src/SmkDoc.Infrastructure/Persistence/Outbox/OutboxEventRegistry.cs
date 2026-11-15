using System.Text.Json;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Events;

namespace SmkDoc.Infrastructure.Persistence.Outbox;

/// <summary>
/// Deterministic explicit type registry mapping stable schema event names to <see cref="IDomainEvent"/> CLR types.
/// Prevents arbitrary type deserialization vulnerabilities when reading untrusted rows from PostgreSQL.
/// </summary>
public static class OutboxEventRegistry
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<Type, string> TypeToName = new()
    {
        [typeof(ApiKeyRevokedDomainEvent)] = "api_key.revoked.v1",
        [typeof(BreakGlassGrantedDomainEvent)] = "break_glass.granted.v1",
        [typeof(BreakGlassRevokedDomainEvent)] = "break_glass.revoked.v1",
        [typeof(DocumentJobCanceledDomainEvent)] = "document_job.canceled.v1",
        [typeof(DocumentJobFailedDomainEvent)] = "document_job.failed.v1",
        [typeof(DocumentJobSucceededDomainEvent)] = "document_job.succeeded.v1",
        [typeof(DocumentRenderedDomainEvent)] = "document.rendered.v1",
        [typeof(DocumentVersionCreatedDomainEvent)] = "document_version.created.v1",
        [typeof(ProjectActivatedDomainEvent)] = "project.activated.v1",
        [typeof(ProjectDeactivatedDomainEvent)] = "project.deactivated.v1",
        [typeof(ProjectProvisionedDomainEvent)] = "project.provisioned.v1",
        [typeof(TemplateCreatedDomainEvent)] = "template.created.v1",
        [typeof(TemplatePublishedDomainEvent)] = "template.published.v1",
        [typeof(UserDeactivatedDomainEvent)] = "user.deactivated.v1"
    };

    private static readonly Dictionary<string, Type> NameToType =
        TypeToName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.Ordinal);

    public static (string EventType, string PayloadJson) Serialize(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var clrType = domainEvent.GetType();
        if (!TypeToName.TryGetValue(clrType, out var eventType))
        {
            throw new InvalidOperationException($"Domain event type '{clrType.FullName}' is not registered in {nameof(OutboxEventRegistry)}.");
        }

        string payloadJson = JsonSerializer.Serialize(domainEvent, clrType, SerializerOptions);
        return (eventType, payloadJson);
    }

    public static IDomainEvent Deserialize(string eventType, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        if (!NameToType.TryGetValue(eventType, out var clrType))
        {
            throw new InvalidOperationException($"Unknown outbox event type discriminator '{eventType}'.");
        }

        return (IDomainEvent?)JsonSerializer.Deserialize(payloadJson, clrType, SerializerOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize outbox event payload for type '{eventType}'.");
    }
}

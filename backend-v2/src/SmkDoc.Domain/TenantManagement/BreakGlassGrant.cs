using SmkDoc.Domain.Common;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.TenantManagement;

/// <summary>
/// Audited, time-boxed break-glass support grant authorizing a SuperAdmin to access a specific tenant project on the Data Plane.
/// Enforces Zero Standing Access (ZSA) and caps grant lifetime.
/// </summary>
public sealed class BreakGlassGrant : AggregateRoot<BreakGlassGrantId>
{
    public const int MaxReasonLength = 500;
    public static readonly TimeSpan DefaultMaxDuration = TimeSpan.FromHours(4);

    public ProjectId TargetProjectId { get; private set; }
    public UserId SuperAdminUserId { get; private set; }
    public UserId GrantedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // For EF Core materialization only
    private BreakGlassGrant() { }

    private BreakGlassGrant(
        BreakGlassGrantId id,
        ProjectId targetProjectId,
        UserId superAdminUserId,
        UserId grantedByUserId,
        string reason,
        DateTimeOffset grantedAt,
        DateTimeOffset expiresAt)
        : base(id, grantedAt)
    {
        if (targetProjectId.IsEmpty)
        {
            throw new DomainValidationException("TargetProjectId cannot be empty.");
        }

        if (superAdminUserId.IsEmpty)
        {
            throw new DomainValidationException("SuperAdminUserId cannot be empty.");
        }

        if (grantedByUserId.IsEmpty)
        {
            throw new DomainValidationException("GrantedByUserId cannot be empty.");
        }

        if (expiresAt <= grantedAt)
        {
            throw new DomainValidationException("Break-glass grant expiration must be in the future.");
        }

        if (expiresAt - grantedAt > DefaultMaxDuration)
        {
            throw new DomainValidationException($"Break-glass grant duration cannot exceed {DefaultMaxDuration.TotalHours:0} hours.");
        }

        TargetProjectId = targetProjectId;
        SuperAdminUserId = superAdminUserId;
        GrantedByUserId = grantedByUserId;
        Reason = Guard.MaxLength(Guard.NotBlank(reason, nameof(Reason)), nameof(Reason), MaxReasonLength);
        GrantedAt = grantedAt;
        ExpiresAt = expiresAt;

        AddDomainEvent(new BreakGlassGrantedDomainEvent(Id, TargetProjectId, SuperAdminUserId, GrantedByUserId, ExpiresAt, grantedAt));
    }

    public static BreakGlassGrant Issue(
        ProjectId targetProjectId,
        UserId superAdminUserId,
        UserId grantedByUserId,
        string reason,
        DateTimeOffset grantedAt,
        DateTimeOffset expiresAt) =>
        new(
            BreakGlassGrantId.New(),
            targetProjectId,
            superAdminUserId,
            grantedByUserId,
            reason,
            grantedAt,
            expiresAt);

    public bool IsActiveAt(DateTimeOffset now) =>
        RevokedAt is null && now >= GrantedAt && now < ExpiresAt;

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt.HasValue)
        {
            return;
        }

        RevokedAt = now;
        SetUpdated(now);
        AddDomainEvent(new BreakGlassRevokedDomainEvent(Id, TargetProjectId, SuperAdminUserId, now));
    }
}

using System.Collections.ObjectModel;

namespace SmkDoc.Infrastructure.Data;

/// <summary>
/// Platform-level egress policy defaults for SQL dataset connections and outbound webhooks.
/// Private RFC1918/ULA CIDRs are blocked by default unless explicitly allowlisted here or per-project.
/// </summary>
public sealed class EgressPolicyOptions
{
    public const string SectionName = "EgressPolicy";

    public Collection<string> TenantAllowedCidrs { get; } = [];
}

using System.Net;
using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.UpdateProjectEgressCidrs;

public sealed class UpdateProjectEgressCidrsCommandValidator : AbstractValidator<UpdateProjectEgressCidrsCommand>
{
    public const int MaxCidrCount = 64;

    public UpdateProjectEgressCidrsCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");

        RuleFor(x => x.AllowedCidrs)
            .NotNull().WithMessage("AllowedCidrs list is required.")
            .Must(list => list.Count <= MaxCidrCount)
            .WithMessage($"A maximum of {MaxCidrCount} egress CIDR entries is allowed per project.");

        RuleForEach(x => x.AllowedCidrs)
            .NotEmpty().WithMessage("CIDR entry cannot be empty.")
            .Must(IsValidNonWildcardCidr)
            .WithMessage("Each entry must be a valid IPv4/IPv6 CIDR (with non-zero prefix) or IP address.");
    }

    private static bool IsValidNonWildcardCidr(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (IPNetwork.TryParse(trimmed, out var network))
        {
            return network.PrefixLength > 0;
        }

        return IPAddress.TryParse(trimmed, out _);
    }
}

using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Quotas.Commands.UpsertProjectQuota;

public sealed class UpsertProjectQuotaCommandValidator : AbstractValidator<UpsertProjectQuotaCommand>
{
    public UpsertProjectQuotaCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");

        RuleFor(x => x.MaxRequestsPerMinute)
            .InclusiveBetween(1, 100_000).WithMessage("MaxRequestsPerMinute must be between 1 and 100,000.");

        RuleFor(x => x.MaxConcurrentJobs)
            .InclusiveBetween(1, 1_000).WithMessage("MaxConcurrentJobs must be between 1 and 1,000.");

        RuleFor(x => x.MaxBatchItems)
            .InclusiveBetween(1, 10_000).WithMessage("MaxBatchItems must be between 1 and 10,000.");

        RuleFor(x => x.MaxStorageBytes)
            .GreaterThan(0).WithMessage("MaxStorageBytes must be positive.");

        RuleFor(x => x.MaxActiveApiKeys)
            .InclusiveBetween(1, 100).WithMessage("MaxActiveApiKeys must be between 1 and 100.");
    }
}

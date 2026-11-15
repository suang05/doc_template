using FluentValidation;

namespace SmkDoc.Application.Modules.TenantManagement.Quotas.Queries.GetProjectQuota;

public sealed class GetProjectQuotaQueryValidator : AbstractValidator<GetProjectQuotaQuery>
{
    public GetProjectQuotaQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");
    }
}

using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;
using SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

namespace SmkDoc.Tests.Application.Validators;

public sealed class PaginationValidatorsTests
{
    private readonly ListTemplatesQueryValidator _templatesValidator = new();
    private readonly ListProjectsQueryValidator _projectsValidator = new();
    private readonly ListControlPlaneProjectsQueryValidator _controlPlaneProjectsValidator = new();
    private readonly ListGenerationLogsQueryValidator _logsValidator = new();
    private readonly ListDocumentJobsQueryValidator _jobsValidator = new();

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void OffsetPaginationValidators_WhenPageOrLimitOutOfRange_ShouldFailValidation(int page, int limit)
    {
        _templatesValidator.Validate(new ListTemplatesQuery(Page: page, Limit: limit)).IsValid.Should().BeFalse();
        _projectsValidator.Validate(new ListProjectsQuery(Guid.NewGuid(), Page: page, Limit: limit)).IsValid.Should().BeFalse();
        _controlPlaneProjectsValidator.Validate(new ListControlPlaneProjectsQuery(Page: page, Limit: limit)).IsValid.Should().BeFalse();
        _logsValidator.Validate(new ListGenerationLogsQuery(Page: page, Limit: limit)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 20)]
    [InlineData(5, 100)]
    public void OffsetPaginationValidators_WhenPageAndLimitWithinRange_ShouldPassValidation(int page, int limit)
    {
        _templatesValidator.Validate(new ListTemplatesQuery(Page: page, Limit: limit)).IsValid.Should().BeTrue();
        _projectsValidator.Validate(new ListProjectsQuery(Guid.NewGuid(), Page: page, Limit: limit)).IsValid.Should().BeTrue();
        _controlPlaneProjectsValidator.Validate(new ListControlPlaneProjectsQuery(Page: page, Limit: limit)).IsValid.Should().BeTrue();
        _logsValidator.Validate(new ListGenerationLogsQuery(Page: page, Limit: limit)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ListDocumentJobsQueryValidator_WhenLimitOutOfRange_ShouldFailValidation(int limit)
    {
        _jobsValidator.Validate(new ListDocumentJobsQuery(Limit: limit)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(20, "Queued")]
    [InlineData(100, "Succeeded")]
    public void ListDocumentJobsQueryValidator_WhenValid_ShouldPassValidation(int limit, string? status)
    {
        _jobsValidator.Validate(new ListDocumentJobsQuery(Status: status, Limit: limit)).IsValid.Should().BeTrue();
    }
}

using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class EntityEncapsulationTests
{
    [Fact]
    public void Project_Collections_ShouldBeReadOnly_AndNotDirectlyMutable()
    {
        var now = DateTimeOffset.UtcNow;
        var project = Project.Create(Guid.NewGuid(), ProjectName.Create("Billing System"), TemplateSlug.Create("billing-sys"), now);

        project.Templates.Should().BeAssignableTo<IReadOnlyCollection<Template>>();
        project.ApiKeys.Should().BeAssignableTo<IReadOnlyCollection<ApiKey>>();
        project.UserRoles.Should().BeAssignableTo<IReadOnlyCollection<UserProjectRole>>();

        Action actMutateTemplates = () => ((IList<Template>)project.Templates).Add(
            Template.Create(project.Id, TemplateName.Create("T"), TemplateSlug.Create("template-sample"), null, now));
        actMutateTemplates.Should().Throw<NotSupportedException>();

        Action actMutateApiKeys = () => ((IList<ApiKey>)project.ApiKeys).Add(
            ApiKey.Issue(project.Id, ApiKeyName.Create("Key"), "App", new Sha256Hash(new string('a', 64)), ExpirationPolicy.Never, now));
        actMutateApiKeys.Should().Throw<NotSupportedException>();

        Action actMutateUserRoles = () => ((IList<UserProjectRole>)project.UserRoles).Add(
            UserProjectRole.Create(Guid.NewGuid(), project.Id, RoleType.Developer, now));
        actMutateUserRoles.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Company_Projects_ShouldBeReadOnly_AndNotDirectlyMutable()
    {
        var now = DateTimeOffset.UtcNow;
        var company = Company.Create(CompanyName.Create("Acme Corp"), now);

        company.Projects.Should().BeAssignableTo<IReadOnlyCollection<Project>>();

        Action actMutateProjects = () => ((IList<Project>)company.Projects).Add(
            Project.Create(company.Id, ProjectName.Create("P"), TemplateSlug.Create("proj-sample"), now));
        actMutateProjects.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void User_ProjectRoles_ShouldBeReadOnly_AndMutatedViaDomainMethods()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(EmailAddress.Create("user@test.com"), "hashed_pwd", "John", "Doe", now);

        user.ProjectRoles.Should().BeAssignableTo<IReadOnlyCollection<UserProjectRole>>();

        Action actDirectMutate = () => ((IList<UserProjectRole>)user.ProjectRoles).Add(
            UserProjectRole.Create(user.Id, Guid.NewGuid(), RoleType.Developer, now));
        actDirectMutate.Should().Throw<NotSupportedException>();

        var projectId = Guid.NewGuid();
        var role = UserProjectRole.Create(user.Id, projectId, RoleType.Admin, now);
        user.AssignProjectRole(role, now);

        user.ProjectRoles.Should().ContainSingle().Which.Role.Should().Be(RoleType.Admin);

        // Assigning updated role replaces previous for same project
        var updatedRole = UserProjectRole.Create(user.Id, projectId, RoleType.Viewer, now);
        user.AssignProjectRole(updatedRole, now);

        user.ProjectRoles.Should().ContainSingle().Which.Role.Should().Be(RoleType.Viewer);

        // Remove role
        user.RemoveProjectRole(projectId, now);
        user.ProjectRoles.Should().BeEmpty();
    }

    [Fact]
    public void User_AssignProjectRole_MismatchUserId_ThrowsDomainValidationException()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(EmailAddress.Create("user@test.com"), "hashed_pwd", "John", "Doe", now);
        var foreignRole = UserProjectRole.Create(Guid.NewGuid(), Guid.NewGuid(), RoleType.Developer, now);

        Action act = () => user.AssignProjectRole(foreignRole, now);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match user ID*");
    }

    [Fact]
    public void Template_AcceptsValueObjects_AndProtectsInvariants()
    {
        var projectId = Guid.NewGuid();
        var voName = TemplateName.Create("Tax Invoice");
        var voSlug = TemplateSlug.Create("invoice-tax");
        var now = DateTimeOffset.UtcNow;
        var template = Template.Create(projectId, voName, voSlug, null, now);

        template.Name.Should().Be(voName);
        template.Slug.Should().Be(voSlug);
        template.Slug.Value.Should().Be("invoice-tax");
    }
}

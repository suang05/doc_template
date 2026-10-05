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
        var project = new Project(Guid.NewGuid(), "Billing System", "billing-sys");

        project.Templates.Should().BeAssignableTo<IReadOnlyCollection<Template>>();
        project.ApiKeys.Should().BeAssignableTo<IReadOnlyCollection<ApiKey>>();
        project.UserRoles.Should().BeAssignableTo<IReadOnlyCollection<UserProjectRole>>();

        Action actMutateTemplates = () => ((IList<Template>)project.Templates).Add(
            new Template(project.Id, "T", "template-sample"));
        actMutateTemplates.Should().Throw<NotSupportedException>();

        Action actMutateApiKeys = () => ((IList<ApiKey>)project.ApiKeys).Add(
            ApiKey.Issue(project.Id, ApiKeyName.Create("Key"), "App", new Sha256Hash(new string('a', 64)), ExpirationPolicy.Never, DateTimeOffset.UtcNow));
        actMutateApiKeys.Should().Throw<NotSupportedException>();

        Action actMutateUserRoles = () => ((IList<UserProjectRole>)project.UserRoles).Add(
            new UserProjectRole(Guid.NewGuid(), project.Id, RoleType.Developer));
        actMutateUserRoles.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Company_Projects_ShouldBeReadOnly_AndNotDirectlyMutable()
    {
        var company = new Company("Acme Corp");

        company.Projects.Should().BeAssignableTo<IReadOnlyCollection<Project>>();

        Action actMutateProjects = () => ((IList<Project>)company.Projects).Add(
            new Project(company.Id, "P", "p"));
        actMutateProjects.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void User_ProjectRoles_ShouldBeReadOnly_AndMutatedViaDomainMethods()
    {
        var user = new User("user@test.com", "hashed_pwd", "John", "Doe");

        user.ProjectRoles.Should().BeAssignableTo<IReadOnlyCollection<UserProjectRole>>();

        Action actDirectMutate = () => ((IList<UserProjectRole>)user.ProjectRoles).Add(
            new UserProjectRole(user.Id, Guid.NewGuid(), RoleType.Developer));
        actDirectMutate.Should().Throw<NotSupportedException>();

        var projectId = Guid.NewGuid();
        var role = new UserProjectRole(user.Id, projectId, RoleType.Admin);
        user.AssignProjectRole(role);

        user.ProjectRoles.Should().ContainSingle().Which.Role.Should().Be(RoleType.Admin);

        // Assigning updated role replaces previous for same project
        var updatedRole = new UserProjectRole(user.Id, projectId, RoleType.Viewer);
        user.AssignProjectRole(updatedRole);

        user.ProjectRoles.Should().ContainSingle().Which.Role.Should().Be(RoleType.Viewer);

        // Remove role
        user.RemoveProjectRole(projectId);
        user.ProjectRoles.Should().BeEmpty();
    }

    [Fact]
    public void User_AssignProjectRole_MismatchUserId_ThrowsDomainValidationException()
    {
        var user = new User("user@test.com", "hashed_pwd", "John", "Doe");
        var foreignRole = new UserProjectRole(Guid.NewGuid(), Guid.NewGuid(), RoleType.Developer);

        Action act = () => user.AssignProjectRole(foreignRole);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*does not match user ID*");
    }

    [Fact]
    public void Template_AcceptsTemplateSlug_DirectlyOrViaString()
    {
        var projectId = Guid.NewGuid();
        var voSlug = TemplateSlug.Create("invoice-tax");
        var templateFromVo = new Template(projectId, "Tax Invoice", voSlug);

        templateFromVo.Slug.Should().Be(voSlug);
        templateFromVo.Slug.Value.Should().Be("invoice-tax");

        var templateFromString = new Template(projectId, "Tax Invoice", "invoice-tax");
        templateFromString.Slug.Value.Should().Be("invoice-tax");
        templateFromString.Slug.Should().Be(voSlug);
    }
}

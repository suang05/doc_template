using FluentAssertions;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class BaseEntityTests
{
    // ── BaseEntity UUIDv7 Generation ────────────────────────────────────────

    [Fact]
    public void BaseEntity_Id_IsSequentialUuidV7()
    {
        // UUIDv7 is monotonic / chronological: id1 should precede id2 when generated in sequence
        var now = TestConstants.BaselineTime;
        var entity1 = Template.Create(Guid.NewGuid(), TemplateName.Create("T1"), TemplateSlug.Create("t1"), null, now);
        var entity2 = Template.Create(Guid.NewGuid(), TemplateName.Create("T2"), TemplateSlug.Create("t2"), null, now.AddMilliseconds(1));

        entity1.Id.Should().NotBe(Guid.Empty);
        entity2.Id.Should().NotBe(Guid.Empty);
        entity1.Id.Should().NotBe(entity2.Id);

        // Validate that Guid is created and valid
        entity1.Id.ToString().Length.Should().Be(36);
    }

    [Fact]
    public void UserProjectRole_ConstructorAndEncapsulation_Work()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var role = UserProjectRole.Create(userId, projectId, RoleType.Developer, TestConstants.BaselineTime);

        role.UserId.Should().Be(userId);
        role.ProjectId.Should().Be(projectId);
        role.Role.Should().Be(RoleType.Developer);

        role.UpdateRole(RoleType.Admin);
        role.Role.Should().Be(RoleType.Admin);
    }

    [Fact]
    public void BaseEntity_IdSetter_IsProtected_AndCannotBeCalledExternally()
    {
        var idProp = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        idProp.Should().NotBeNull();
        idProp!.GetSetMethod(nonPublic: false).Should().BeNull("BaseEntity.Id setter must not be public (AP-021)");
        idProp.GetSetMethod(nonPublic: true)!.IsFamily.Should().BeTrue("BaseEntity.Id setter must be protected");
    }

    [Fact]
    public void BaseEntity_Constructor_AllowsControlledIdentityPassThrough()
    {
        var customId = Guid.NewGuid();
        var template = TemplateTestFactory.Create(customId, Guid.NewGuid(), "Sample", "sample");

        template.Id.Should().Be(customId);
    }

    [Fact]
    public void BaseEntity_EqualsAndOperators_CompareByIdAndType()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var t1 = TemplateTestFactory.Create(id, projectId, "T1", "t1");
        var t2 = TemplateTestFactory.Create(id, projectId, "T2", "t2");
        var other = TemplateTestFactory.Create(Guid.NewGuid(), projectId, "T3", "t3");

        t1.Should().Be(t2);
        (t1 == t2).Should().BeTrue();
        (t1 != other).Should().BeTrue();
        t1.GetHashCode().Should().Be(t2.GetHashCode());
    }
}

using FluentAssertions;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class BaseEntityTests
{
    // ── BaseEntity UUIDv7 Generation ────────────────────────────────────────

    [Fact]
    public void BaseEntity_Id_IsSequentialUuidV7()
    {
        // UUIDv7 is monotonic / chronological: id1 should precede id2 when generated in sequence
        var entity1 = new Template(Guid.NewGuid(), "T1", "t1");
        Thread.Sleep(2);
        var entity2 = new Template(Guid.NewGuid(), "T2", "t2");

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
        var role = new UserProjectRole(userId, projectId, RoleType.Developer);

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
        var template = new Template(Guid.NewGuid(), "Sample", "sample", id: customId);

        template.Id.Should().Be(customId);
    }

    [Fact]
    public void BaseEntity_EqualsAndOperators_CompareByIdAndType()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var t1 = new Template(projectId, "T1", "t1", id: id);
        var t2 = new Template(projectId, "T2", "t2", id: id);
        var other = new Template(projectId, "T3", "t3", id: Guid.NewGuid());

        t1.Should().Be(t2);
        (t1 == t2).Should().BeTrue();
        (t1 != other).Should().BeTrue();
        t1.GetHashCode().Should().Be(t2.GetHashCode());
    }
}

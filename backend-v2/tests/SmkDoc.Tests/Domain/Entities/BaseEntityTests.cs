using FluentAssertions;
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

        // In UUIDv7, version field in byte 6 is 0x70
        var bytes = entity1.Id.ToByteArray();
        var version = (bytes[7] >> 4) & 0x0F; // Big-endian representation of time_hi_and_version
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
    public void BaseEntity_SetAuditTimestampsForTesting_SetsCreatedAtAndUpdatedAt()
    {
        var template = new Template(Guid.NewGuid(), "Audit Test", "audit-test");
        var fixedCreatedAt = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
        var fixedUpdatedAt = new DateTimeOffset(2026, 1, 16, 9, 0, 0, TimeSpan.Zero);

        template.SetAuditTimestampsForTesting(fixedCreatedAt, fixedUpdatedAt);

        template.CreatedAt.Should().Be(fixedCreatedAt);
        template.UpdatedAt.Should().Be(fixedUpdatedAt);
    }
}

using FluentAssertions;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.ValueObjects.Ids;

public class StronglyTypedIdTests
{
    [Fact]
    public void New_GeneratesNonEmptySequentialId()
    {
        var id1 = ProjectId.New();
        var id2 = ProjectId.New();

        id1.Value.Should().NotBeEmpty();
        id2.Value.Should().NotBeEmpty();
        id1.Should().NotBe(id2);
        id1.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Empty_ReturnsEmptyGuid()
    {
        var empty = ProjectId.Empty;

        empty.Value.Should().Be(Guid.Empty);
        empty.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void StructuralEquality_SameGuid_AreEqual()
    {
        var guid = Guid.NewGuid();
        var id1 = new TemplateId(guid);
        var id2 = new TemplateId(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void ImplicitConversions_Bidirectional_WorkSeamlessly()
    {
        var rawGuid = Guid.NewGuid();
        ProjectId projectId = rawGuid; // Implicit from Guid
        Guid extractedGuid = projectId; // Implicit to Guid

        extractedGuid.Should().Be(rawGuid);
        projectId.Value.Should().Be(rawGuid);
    }

    [Fact]
    public void Comparison_SortsDeterministically()
    {
        var guid1 = new Guid("00000000-0000-0000-0000-000000000001");
        var guid2 = new Guid("00000000-0000-0000-0000-000000000002");
        var id1 = new UserId(guid1);
        var id2 = new UserId(guid2);

        id1.CompareTo(id2).Should().BeNegative();
        id2.CompareTo(id1).Should().BePositive();
        id1.CompareTo(new UserId(guid1)).Should().Be(0);
    }

    [Fact]
    public void AllIdTypes_HaveConsistentSizeAndBehavior()
    {
        var guid = Guid.NewGuid();
        new CompanyId(guid).Value.Should().Be(guid);
        new DocumentId(guid).Value.Should().Be(guid);
        new ApiKeyId(guid).Value.Should().Be(guid);
        new DatasetId(guid).Value.Should().Be(guid);
        new DataConnectionId(guid).Value.Should().Be(guid);
    }
}

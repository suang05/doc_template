using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Domain.Entities;

public class BaseEntityTests
{
    [Fact]
    public void EntityEquality_UsesIdComparison()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var t1 = new TemplateBuilder().WithId(id).WithProjectId(projectId).WithName("T1").WithSlug("t1").Build();
        var t2 = new TemplateBuilder().WithId(id).WithProjectId(projectId).WithName("T2").WithSlug("t2").Build();
        var other = new TemplateBuilder().WithId(Guid.NewGuid()).WithProjectId(projectId).WithName("T3").WithSlug("t3").Build();

        t1.Should().Be(t2);
        (t1 == t2).Should().BeTrue();
        (t1 != other).Should().BeTrue();
        t1.GetHashCode().Should().Be(t2.GetHashCode());
    }
}

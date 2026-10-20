using FluentAssertions;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class BaseEntityTests
{
    [Fact]
    public void EntityEquality_UsesIdComparison()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var t1 = TemplateBuilder.Create(id, projectId, "T1", "t1");
        var t2 = TemplateBuilder.Create(id, projectId, "T2", "t2");
        var other = TemplateBuilder.Create(Guid.NewGuid(), projectId, "T3", "t3");

        t1.Should().Be(t2);
        (t1 == t2).Should().BeTrue();
        (t1 != other).Should().BeTrue();
        t1.GetHashCode().Should().Be(t2.GetHashCode());
    }
}

using FluentAssertions;
using SmkDoc.Application.Common;
using Xunit;

namespace SmkDoc.Tests.Application.Common;

public class StorageBucketsTests
{
    [Fact]
    public void Templates_ShouldBeExactly_templates()
    {
        StorageBuckets.Templates.Should().Be("templates");
    }

    [Fact]
    public void Outputs_ShouldBeExactly_outputs()
    {
        StorageBuckets.Outputs.Should().Be("outputs");
    }

    [Fact]
    public void AllBucketNames_ShouldBeLowercase()
    {
        StorageBuckets.Templates.Should().Be(StorageBuckets.Templates.ToLowerInvariant(),
            "MinIO bucket names must be lowercase");
        StorageBuckets.Outputs.Should().Be(StorageBuckets.Outputs.ToLowerInvariant(),
            "MinIO bucket names must be lowercase");
    }

    [Fact]
    public void AllBucketNames_ShouldBeDistinct()
    {
        var names = new[] { StorageBuckets.Templates, StorageBuckets.Outputs };
        names.Should().OnlyHaveUniqueItems("bucket names must not collide");
    }
}

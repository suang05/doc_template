using FluentAssertions;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Domain.Enums;

public class DatabaseProviderTests
{
    [Fact]
    public void PredefinedValues_AreCorrect()
    {
        DatabaseProvider.PostgreSQL.Id.Should().Be(1);
        DatabaseProvider.PostgreSQL.Name.Should().Be("PostgreSQL");

        DatabaseProvider.SqlServer.Id.Should().Be(2);
        DatabaseProvider.SqlServer.Name.Should().Be("SqlServer");

        DatabaseProvider.MySQL.Id.Should().Be(3);
        DatabaseProvider.MySQL.Name.Should().Be("MySQL");

        DatabaseProvider.Oracle.Id.Should().Be(4);
        DatabaseProvider.Oracle.Name.Should().Be("Oracle");
    }

    [Theory]
    [InlineData("PostgreSQL", 1)]
    [InlineData("postgresql", 1)]
    [InlineData("SqlServer", 2)]
    [InlineData("sqlserver", 2)]
    [InlineData("MySQL", 3)]
    [InlineData("Oracle", 4)]
    public void FromDisplayName_CaseInsensitive_ResolvesCorrectProvider(string name, int expectedId)
    {
        var provider = Enumeration.FromDisplayName<DatabaseProvider>(name);
        provider.Id.Should().Be(expectedId);
    }

    [Theory]
    [InlineData(1, "PostgreSQL")]
    [InlineData(2, "SqlServer")]
    [InlineData(3, "MySQL")]
    [InlineData(4, "Oracle")]
    public void FromValue_ResolvesCorrectProvider(int id, string expectedName)
    {
        var provider = Enumeration.FromValue<DatabaseProvider>(id);
        provider.Name.Should().Be(expectedName);
    }

    [Fact]
    public void FromDisplayName_WithUnknownName_ThrowsDomainValidationException()
    {
        var act = () => Enumeration.FromDisplayName<DatabaseProvider>("Sqlite");
        act.Should().Throw<DomainValidationException>();
    }
}

using System.Net;
using Microsoft.Extensions.Configuration;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Infrastructure.Data;

namespace SmkDoc.Tests.Infrastructure.Data;

public sealed class SqlSecurityGuardTests
{
    private static SqlSecurityGuard CreateSut(
        string? defaultConn = "Host=10.20.30.40;Port=5432;Database=smk_doc_internal;Username=app;Password=secret",
        Func<string, CancellationToken, Task<IPAddress[]>>? dnsResolver = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = defaultConn
            })
            .Build();

        return new SqlSecurityGuard(
            config,
            dnsResolver ?? ((_, _) => Task.FromResult(new[] { IPAddress.Parse("10.10.1.50") })));
    }

    [Theory]
    [InlineData("PostgreSQL", "Host=localhost;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=api.localhost;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=127.0.0.1;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=::1;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=0.0.0.0;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=169.254.169.254;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=fe80::1;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=10.0.0.1,10.0.0.2;Database=erp;Username=u;Password=p")]
    [InlineData("PostgreSQL", "Host=10.20.30.40;Port=5432;Database=smk_doc_internal;Username=ro;Password=p")]
    public async Task ValidateConnectionTargetAsync_WithProhibitedSsrfOrSelfTarget_ThrowsForbiddenException(
        string provider,
        string connectionString)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.ValidateConnectionTargetAsync(provider, connectionString);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ValidateConnectionTargetAsync_WhenHostnameResolvesToLoopback_ThrowsForbiddenException()
    {
        // Arrange: DNS rebinding defense
        var sut = CreateSut(dnsResolver: (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

        // Act
        Func<Task> act = () => sut.ValidateConnectionTargetAsync(
            "PostgreSQL",
            "Host=evil-rebind.example.com;Database=erp;Username=u;Password=p");

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*loopback*");
    }

    [Theory]
    [InlineData("PostgreSQL", "Host=10.10.1.5;Port=5432;Database=customer_erp;Username=reader;Password=secret")]
    [InlineData("PostgreSQL", "Host=192.168.1.20;Port=5432;Database=warehouse;Username=reader;Password=secret")]
    [InlineData("SqlServer", "Server=172.16.5.10,1433;Database=BillingDb;User Id=reader;Password=secret")]
    public async Task ValidateConnectionTargetAsync_WithValidPrivateRfc1918ExternalTarget_Succeeds(
        string provider,
        string connectionString)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.ValidateConnectionTargetAsync(provider, connectionString);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("SELECT id, total FROM invoices WHERE id = @invoiceId")]
    [InlineData("SELECT id FROM orders;")]
    [InlineData("WITH summary AS (SELECT customer_id, SUM(amount) AS total FROM invoices GROUP BY customer_id) SELECT * FROM summary")]
    [InlineData("/* header comment */ SELECT 'DROP TABLE fake_literal' AS note FROM customers -- trailing comment")]
    public void ValidateReadOnlySql_WithValidSelectOrCteQuery_Succeeds(string sql)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Action act = () => sut.ValidateReadOnlySql(sql);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("DROP TABLE users")]
    [InlineData("UPDATE users SET is_active = false")]
    [InlineData("DELETE FROM templates")]
    [InlineData("INSERT INTO logs VALUES (1)")]
    [InlineData("COPY users TO '/tmp/dump.csv'")]
    [InlineData("SET role postgres")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT pg_sleep(30)")]
    [InlineData("SELECT $$dollar_quoted$$")]
    [InlineData("WITH d AS (DELETE FROM users RETURNING *) SELECT * FROM d")]
    public void ValidateReadOnlySql_WithMutatingStackedOrDangerousQuery_ThrowsDomainValidationException(string maliciousSql)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Action act = () => sut.ValidateReadOnlySql(maliciousSql);

        // Assert
        act.Should().Throw<DomainValidationException>();
    }
}

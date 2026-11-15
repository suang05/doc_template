using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Data;

namespace SmkDoc.Tests.Infrastructure.Data;

public sealed class ConnectionHostValidatorTests
{
    private readonly ISecurityAuditService _securityAudit = Substitute.For<ISecurityAuditService>();
    private readonly IProjectStatusReader _projectStatusReader = Substitute.For<IProjectStatusReader>();

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?>? entries = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries ?? new Dictionary<string, string?>())
            .Build();

    private ConnectionHostValidator CreateValidator(
        Func<string, CancellationToken, Task<IPAddress[]>> dnsResolver,
        IReadOnlyDictionary<string, string?>? configEntries = null,
        IEnumerable<string>? tenantAllowedCidrs = null,
        IProjectStatusReader? projectStatusReader = null)
    {
        var egressOptions = new EgressPolicyOptions();
        if (tenantAllowedCidrs is not null)
        {
            foreach (var cidr in tenantAllowedCidrs)
            {
                egressOptions.TenantAllowedCidrs.Add(cidr);
            }
        }

        return new ConnectionHostValidator(
            BuildConfiguration(configEntries),
            Options.Create(egressOptions),
            projectStatusReader ?? _projectStatusReader,
            _securityAudit,
            dnsResolver);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    public async Task ResolveAndValidateHostAsync_WhenAlwaysProhibitedIpLiteral_ThrowsSecurityViolationAndAudits(string literalIp)
    {
        var projectId = ProjectId.New();
        var sut = CreateValidator(
            (_, _) => Task.FromResult(Array.Empty<IPAddress>()),
            tenantAllowedCidrs: ["0.0.0.0/0", "::/0"]);

        var act = () => sut.ResolveAndValidateHostAsync(literalIp, 5432, projectId, CancellationToken.None);

        await act.Should().ThrowAsync<SecurityViolationException>();
        _securityAudit.Received(1).LogEgressDenied(projectId.Value, "http", Arg.Any<string>());
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("api.localhost")]
    [InlineData("postgres")]
    [InlineData("redis")]
    [InlineData("minio")]
    [InlineData("gotenberg")]
    [InlineData("metadata.google.internal")]
    [InlineData("host.docker.internal")]
    public async Task ResolveAndValidateHostAsync_WhenProhibitedInternalHostname_ThrowsSecurityViolation(string hostname)
    {
        var sut = CreateValidator((_, _) => Task.FromResult<IPAddress[]>([IPAddress.Parse("93.184.216.34")]));

        var act = () => sut.ResolveAndValidateHostAsync(hostname, 443, null, CancellationToken.None);

        await act.Should().ThrowAsync<SecurityViolationException>();
    }

    [Fact]
    public async Task ResolveAndValidateHostAsync_WhenControlPlaneDatabaseHostTargeted_ThrowsSecurityViolation()
    {
        var sut = CreateValidator(
            (host, _) => Task.FromResult<IPAddress[]>(
                host == "pg-primary.corp.internal"
                    ? [IPAddress.Parse("198.18.0.1")]
                    : [IPAddress.Parse("93.184.216.34")]),
            configEntries: new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=pg-primary.corp.internal;Database=smk_doc"
            });

        var act = () => sut.ResolveAndValidateHostAsync("pg-primary.corp.internal", 5432, null, CancellationToken.None);

        await act.Should().ThrowAsync<SecurityViolationException>();
    }

    [Fact]
    public async Task ResolveAndValidateHostAsync_WhenDnsRebindingReturnsMixedPublicAndPrivateIp_ThrowsSecurityViolation()
    {
        var sut = CreateValidator((_, _) => Task.FromResult(new[]
        {
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("10.1.2.3")
        }));

        var act = () => sut.ResolveAndValidateHostAsync("rebinding.vendor.test", 443, null, CancellationToken.None);

        await act.Should().ThrowAsync<SecurityViolationException>();
    }

    [Fact]
    public async Task ResolveAndValidateHostAsync_WhenPublicHostResolvesCleanly_ReturnsPinnedPublicAddresses()
    {
        var sut = CreateValidator((_, _) => Task.FromResult(new[]
        {
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("93.184.216.34")
        }));

        var result = await sut.ResolveAndValidateHostAsync("WEBHOOKS.Example.Com.", 443, null, CancellationToken.None);

        result.NormalizedHost.Should().Be("WEBHOOKS.Example.Com");
        result.Port.Should().Be(443);
        result.PinnedAddresses.Should().ContainSingle().Which.Should().Be(IPAddress.Parse("93.184.216.34"));
    }

    [Fact]
    public async Task ResolveAndValidateHostAsync_WhenPrivateRfc1918BlockedByDefaultButAllowlistedPerProject_AllowsApprovedSubnet()
    {
        var projectId = ProjectId.New();
        _projectStatusReader.FindProjectStatusAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectStatusSnapshot(projectId, "erp", true, ["10.20.0.0/16"]));

        var sut = CreateValidator((_, _) => Task.FromResult<IPAddress[]>([IPAddress.Parse("10.20.5.12")]));

        var allowed = await sut.ResolveAndValidateHostAsync("erp-db.internal-vpn.example", 5432, projectId, CancellationToken.None);
        allowed.PinnedAddresses.Should().ContainSingle().Which.Should().Be(IPAddress.Parse("10.20.5.12"));

        var blockedOtherProject = () => sut.ResolveAndValidateHostAsync("erp-db.internal-vpn.example", 5432, ProjectId.New(), CancellationToken.None);
        await blockedOtherProject.Should().ThrowAsync<SecurityViolationException>();
    }

    [Fact]
    public async Task ValidateConnectionTargetAsync_WhenPostgresAndSqlServerValid_ExtractsHostAndPortAndPinsIp()
    {
        var sut = CreateValidator((_, _) => Task.FromResult<IPAddress[]>([IPAddress.Parse("93.184.216.34")]));

        var pg = await sut.ValidateConnectionTargetAsync(
            "PostgreSQL",
            "Host=db.vendor.example;Port=5433;Database=erp;Username=ro;Password=secret",
            null,
            CancellationToken.None);

        pg.NormalizedHost.Should().Be("db.vendor.example");
        pg.Port.Should().Be(5433);
        pg.PinnedAddresses.Should().ContainSingle().Which.Should().Be(IPAddress.Parse("93.184.216.34"));

        var mssql = await sut.ValidateConnectionTargetAsync(
            "SqlServer",
            "Server=tcp:sql.vendor.example,14330;Database=erp;User Id=ro;Password=secret",
            null,
            CancellationToken.None);

        mssql.NormalizedHost.Should().Be("sql.vendor.example");
        mssql.Port.Should().Be(14330);
    }

    [Theory]
    [InlineData("SELECT id, name FROM orders WHERE status = 'INSERT INTO' -- DROP TABLE users")]
    [InlineData("WITH cte AS (SELECT id FROM invoices) SELECT * FROM cte /* DELETE FROM x */")]
    public void ValidateReadOnlySql_WhenReadOnlySelectOrCte_DoesNotThrow(string sql)
    {
        var sut = CreateValidator((_, _) => Task.FromResult(Array.Empty<IPAddress>()));

        var act = () => sut.ValidateReadOnlySql(sql);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("DELETE FROM users")]
    [InlineData("INSERT INTO orders(id) VALUES (1)")]
    [InlineData("SELECT 1; DROP TABLE users")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("DO $$ BEGIN PERFORM 1; END $$")]
    [InlineData("SELECT 'unclosed string")]
    [InlineData("SELECT 1 /* unclosed comment")]
    public void ValidateReadOnlySql_WhenMutatingStackedDollarQuotedOrMalformed_ThrowsDomainValidationException(string sql)
    {
        var sut = CreateValidator((_, _) => Task.FromResult(Array.Empty<IPAddress>()));

        var act = () => sut.ValidateReadOnlySql(sql);

        act.Should().Throw<DomainValidationException>();
    }
}

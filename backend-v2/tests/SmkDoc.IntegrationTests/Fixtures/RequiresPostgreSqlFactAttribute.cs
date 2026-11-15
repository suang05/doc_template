using System.IO.Pipes;
using System.Net.Sockets;

namespace SmkDoc.IntegrationTests.Fixtures;

/// <summary>
/// Marks integration tests that require a live PostgreSQL 17 instance (either via
/// <c>SMK_TEST_PG_CONNECTION</c> or a running Docker daemon for Testcontainers).
/// Skips honestly when Docker/PostgreSQL is unavailable locally without falsely reporting a pass.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresPostgreSqlFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> CachedSkipReason = new(EvaluateSkipReason);

    public RequiresPostgreSqlFactAttribute()
    {
        if (CachedSkipReason.Value is { Length: > 0 } reason)
        {
            Skip = reason;
        }
    }

    private static string? EvaluateSkipReason()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMK_TEST_PG_CONNECTION")) ||
            string.Equals(Environment.GetEnvironmentVariable("SMK_TEST_PG_USE_CONTAINERS"), "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (IsDockerReachable() || IsLocalPostgresListening())
        {
            return null;
        }

        return "Requires PostgreSQL 17: start Docker Desktop or set SMK_TEST_PG_CONNECTION / SMK_TEST_PG_USE_CONTAINERS=true.";
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Host Docker socket probing catches platform-specific pipe/socket/IO failures.")]
    private static bool IsDockerReachable()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string[] pipes = ["dockerDesktopLinuxEngine", "docker_engine"];
                foreach (var pipeName in pipes)
                {
                    try
                    {
                        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                        pipe.Connect(300);
                        return true;
                    }
                    catch
                    {
                        // Probe next Windows Docker pipe.
                    }
                }

                return false;
            }

            if (File.Exists("/var/run/docker.sock"))
            {
                return true;
            }

            string? dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
            return !string.IsNullOrWhiteSpace(dockerHost);
        }
        catch
        {
            return false;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Localhost TCP probe returns false on timeout or connection refusal.")]
    private static bool IsLocalPostgresListening()
    {
        try
        {
            using var tcp = new TcpClient();
            var task = tcp.ConnectAsync("127.0.0.1", 5432);
            return task.Wait(200) && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }
}

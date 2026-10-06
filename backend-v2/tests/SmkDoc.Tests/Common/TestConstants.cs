namespace SmkDoc.Tests.Common;

/// <summary>
/// Shared test constants providing deterministic baselines across all test suites.
/// Prevents temporal drift and race conditions caused by runtime calls to DateTimeOffset.UtcNow.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Deterministic baseline timestamp used across unit test fixtures and builders.
    /// </summary>
    public static readonly DateTimeOffset BaselineTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Default project identifier used for tenant isolation tests.
    /// </summary>
    public static readonly Guid DefaultProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Default user identifier used for authorization/audit tests.
    /// </summary>
    public static readonly Guid DefaultUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

namespace SmkDocServer.Domain.Models;

/// <summary>
/// Strongly-typed settings for the ReportBro Python server (Options Pattern — Rule 13).
/// Bound from appsettings.json section "ReportBro".
/// </summary>
public class ReportBroSettings
{
    public string BaseUrl { get; set; } = "http://reportbro:5000";

    /// <summary>Seconds to wait for a report to finish generating before timing out.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}

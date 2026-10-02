using System.Diagnostics;
using System.Diagnostics.Metrics;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Observability;

/// <summary>
/// Cloud-Native APM Metrics and Tracing provider using .NET 10 System.Diagnostics.Metrics.
/// Instruments are automatically discoverable by OpenTelemetry, Prometheus, dotnet-counters, and Grafana.
/// </summary>
public sealed class DocumentMetrics : IDocumentMetrics, IDisposable
{
    public const string MeterName = "SmkDoc.DocumentEngine";
    public const string MeterVersion = "2.0.0";

    public static readonly Meter Meter = new(MeterName, MeterVersion);
    public static readonly ActivitySource ActivitySource = new(MeterName, MeterVersion);

    private readonly Histogram<double> _durationHistogram;
    private readonly Counter<long> _totalCounter;
    private readonly Histogram<long> _bytesHistogram;
    private readonly Counter<long> _cacheCounter;

    public DocumentMetrics()
    {
        _durationHistogram = Meter.CreateHistogram<double>(
            "document.render.duration",
            unit: "ms",
            description: "Duration of document generation in milliseconds");

        _totalCounter = Meter.CreateCounter<long>(
            "document.render.total",
            unit: "documents",
            description: "Total number of document generation requests");

        _bytesHistogram = Meter.CreateHistogram<long>(
            "document.render.bytes",
            unit: "bytes",
            description: "Output file size in bytes");

        _cacheCounter = Meter.CreateCounter<long>(
            "template.cache.requests",
            unit: "requests",
            description: "Compiled template cache access count");
    }

    public void RecordGenerationSuccess(string engineType, string outputFormat, double durationMs, long bytes)
    {
        var tags = new TagList
        {
            { "engine_type", engineType },
            { "output_format", outputFormat },
            { "status", "SUCCESS" }
        };

        _durationHistogram.Record(durationMs, tags);
        _totalCounter.Add(1, tags);

        if (bytes > 0)
        {
            _bytesHistogram.Record(bytes, tags);
        }
    }

    public void RecordGenerationFailure(string engineType, string outputFormat, double durationMs, string failureReason)
    {
        var tags = new TagList
        {
            { "engine_type", engineType },
            { "output_format", outputFormat },
            { "status", "FAILED" },
            { "failure_reason", failureReason }
        };

        _durationHistogram.Record(durationMs, tags);
        _totalCounter.Add(1, tags);
    }

    public void RecordCacheRequest(bool isHit)
    {
        var tags = new TagList
        {
            { "cache_hit", isHit ? "true" : "false" }
        };

        _cacheCounter.Add(1, tags);
    }

    public void Dispose()
    {
        // Meter is static and lifetime spans the application run
    }
}

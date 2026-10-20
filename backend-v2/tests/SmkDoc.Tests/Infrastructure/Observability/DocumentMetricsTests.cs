using System.Diagnostics.Metrics;
using FluentAssertions;
using SmkDoc.Infrastructure.Observability;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Observability;

public class DocumentMetricsTests
{
    [Fact]
    public void RecordGenerationSuccess_ShouldEmitExpectedInstrumentsAndTags()
    {
        using var recorder = new MetricRecorder(DocumentMetrics.MeterName);
        using var sut = new DocumentMetrics();

        sut.RecordGenerationSuccess("Html", "pdf", 125.5, 1024);

        recorder.Measurements.Should().ContainSingle(m => m.Instrument == "document.render.duration" && (double)m.Measurement == 125.5)
            .Which.Tags.Should().Contain(new KeyValuePair<string, object?>("engine_type", "Html"))
            .And.Contain(new KeyValuePair<string, object?>("output_format", "pdf"))
            .And.Contain(new KeyValuePair<string, object?>("status", "SUCCESS"));

        recorder.Measurements.Should().ContainSingle(m => m.Instrument == "document.render.total" && (long)m.Measurement == 1);
        recorder.Measurements.Should().ContainSingle(m => m.Instrument == "document.render.bytes" && (long)m.Measurement == 1024);
    }

    [Fact]
    public void RecordGenerationFailure_ShouldEmitFailedStatusAndReason()
    {
        using var recorder = new MetricRecorder(DocumentMetrics.MeterName);
        using var sut = new DocumentMetrics();

        sut.RecordGenerationFailure("Excel", "xlsx", 45.0, "VALIDATION_FAILED");

        recorder.Measurements.Should().ContainSingle(m => m.Instrument == "document.render.total")
            .Which.Tags.Should().Contain(new KeyValuePair<string, object?>("status", "FAILED"))
            .And.Contain(new KeyValuePair<string, object?>("failure_reason", "VALIDATION_FAILED"))
            .And.Contain(new KeyValuePair<string, object?>("engine_type", "Excel"));
    }

    [Fact]
    public void RecordCacheRequest_ShouldEmitCacheHitOrMiss()
    {
        using var recorder = new MetricRecorder(DocumentMetrics.MeterName);
        using var sut = new DocumentMetrics();

        sut.RecordCacheRequest(isHit: true);
        sut.RecordCacheRequest(isHit: false);

        var hits = recorder.Measurements
            .Where(m => m.Instrument == "template.cache.requests")
            .Select(m => m.Tags["cache_hit"]?.ToString())
            .ToList();

        hits.Should().Contain(["true", "false"]);
    }

    private sealed class MetricRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<(string Instrument, object Measurement, Dictionary<string, object?> Tags)> Measurements { get; } = [];

        public MetricRecorder(string meterName)
        {
            _listener.InstrumentPublished = (inst, l) =>
            {
                if (inst.Meter.Name == meterName)
                {
                    l.EnableMeasurementEvents(inst);
                }
            };
            _listener.SetMeasurementEventCallback<double>((inst, val, tags, _) =>
                Measurements.Add((inst.Name, val, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
            _listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
                Measurements.Add((inst.Name, val, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}

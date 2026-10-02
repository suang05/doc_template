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
        // Arrange
        using var metrics = new DocumentMetrics();
        using var meterListener = new MeterListener();

        bool durationRecorded = false;
        bool totalRecorded = false;
        bool bytesRecorded = false;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == DocumentMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "document.render.duration")
            {
                durationRecorded = true;
                measurement.Should().Be(125.5);
                var tagDict = tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString());
                tagDict["engine_type"].Should().Be("Html");
                tagDict["output_format"].Should().Be("pdf");
                tagDict["status"].Should().Be("SUCCESS");
            }
        });

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "document.render.total")
            {
                totalRecorded = true;
                measurement.Should().Be(1);
            }
            else if (instrument.Name == "document.render.bytes")
            {
                bytesRecorded = true;
                measurement.Should().Be(1024);
            }
        });

        meterListener.Start();

        // Act
        metrics.RecordGenerationSuccess("Html", "pdf", 125.5, 1024);

        // Assert
        durationRecorded.Should().BeTrue();
        totalRecorded.Should().BeTrue();
        bytesRecorded.Should().BeTrue();
    }

    [Fact]
    public void RecordGenerationFailure_ShouldEmitFailedStatusAndReason()
    {
        // Arrange
        using var metrics = new DocumentMetrics();
        using var meterListener = new MeterListener();

        bool failedEventObserved = false;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == DocumentMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "document.render.total")
            {
                var tagDict = tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString());
                if (tagDict.GetValueOrDefault("status") == "FAILED")
                {
                    failedEventObserved = true;
                    tagDict["failure_reason"].Should().Be("VALIDATION_FAILED");
                    tagDict["engine_type"].Should().Be("Excel");
                }
            }
        });

        meterListener.Start();

        // Act
        metrics.RecordGenerationFailure("Excel", "xlsx", 45.0, "VALIDATION_FAILED");

        // Assert
        failedEventObserved.Should().BeTrue();
    }

    [Fact]
    public void RecordCacheRequest_ShouldEmitCacheHitOrMiss()
    {
        // Arrange
        using var metrics = new DocumentMetrics();
        using var meterListener = new MeterListener();

        var observedHits = new List<string?>();

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == DocumentMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "template.cache.requests")
            {
                var tagDict = tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString());
                observedHits.Add(tagDict.GetValueOrDefault("cache_hit"));
            }
        });

        meterListener.Start();

        // Act
        metrics.RecordCacheRequest(isHit: true);
        metrics.RecordCacheRequest(isHit: false);

        // Assert
        observedHits.Should().Contain("true");
        observedHits.Should().Contain("false");
    }
}

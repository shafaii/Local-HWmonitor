using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// A time-bucketed aggregation sample containing statistical summary metrics.
/// </summary>
public sealed record AggregatedReading
{
    public DateTimeOffset TimestampUtc { get; init; }
    public double Average { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public int SampleCount { get; init; }

    public double Range => Max - Min;
}

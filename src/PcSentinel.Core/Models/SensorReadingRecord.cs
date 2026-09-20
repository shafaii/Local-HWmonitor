using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// A lightweight telemetry sensor sample record ready for batched persistence.
/// </summary>
public readonly record struct SensorReadingRecord
{
    public long SensorDefinitionId { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public double Value { get; init; }
    public string SessionId { get; init; }

    public SensorReadingRecord(long sensorDefinitionId, DateTimeOffset timestampUtc, double value, string sessionId)
    {
        SensorDefinitionId = sensorDefinitionId;
        TimestampUtc = timestampUtc;
        Value = value;
        SessionId = sessionId;
    }
}

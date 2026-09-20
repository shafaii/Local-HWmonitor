using System;
using System.Collections.Generic;
using System.Linq;

namespace PcSentinel.Core.Models;

public enum ObservationSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum ObservationType
{
    HighTemperature,
    RapidTemperatureIncrease,
    SustainedHighUtilization,
    PossibleThermalThrottling,
    SensorUnavailable,
    VoltageAnomaly
}

/// <summary>
/// A deterministic observation produced by local trend analysis across telemetry samples.
/// </summary>
public sealed class TelemetryObservation
{
    public Guid ObservationId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required ObservationType Type { get; init; }
    public required ObservationSeverity Severity { get; init; }
    public required string HardwareId { get; init; }
    public required string SensorId { get; init; }
    public required string HardwareName { get; init; }
    public required string SensorName { get; init; }
    public required string Message { get; init; }
    public double? CurrentValue { get; init; }
    public double? Threshold { get; init; }
    public double? DurationSeconds { get; init; }
    public string Unit { get; init; } = string.Empty;

    public override string ToString() => $"[{Severity}] {Type} on {HardwareName} / {SensorName}: {Message}";
}

/// <summary>
/// Fixed-size sliding telemetry window for tracking deterministic statistical trends and rates of change.
/// </summary>
public sealed class TelemetryWindow
{
    private readonly int _maxSamples;
    private readonly Queue<(DateTimeOffset Time, double Value)> _samples = new();

    public TelemetryWindow(int maxSamples = 60)
    {
        _maxSamples = Math.Max(5, maxSamples);
    }

    public void AddSample(DateTimeOffset timestamp, double value)
    {
        _samples.Enqueue((timestamp, value));
        while (_samples.Count > _maxSamples)
        {
            _samples.Dequeue();
        }
    }

    public int Count => _samples.Count;
    public double? LatestValue => _samples.Count > 0 ? _samples.Last().Value : null;
    public double? Average => _samples.Count > 0 ? _samples.Average(s => s.Value) : null;
    public double? Min => _samples.Count > 0 ? _samples.Min(s => s.Value) : null;
    public double? Max => _samples.Count > 0 ? _samples.Max(s => s.Value) : null;

    /// <summary>
    /// Computes the average rate of change per second (dV/dt) between the oldest and newest sample in the window.
    /// </summary>
    public double? RateOfChangePerSecond()
    {
        if (_samples.Count < 2) return null;

        var oldest = _samples.First();
        var newest = _samples.Last();
        double elapsedSec = (newest.Time - oldest.Time).TotalSeconds;

        if (elapsedSec <= 0.1) return 0.0;
        return (newest.Value - oldest.Value) / elapsedSec;
    }

    /// <summary>
    /// Calculates the number of consecutive seconds the most recent readings have stayed above a given threshold.
    /// </summary>
    public double SustainedDurationAbove(double threshold)
    {
        if (_samples.Count == 0) return 0.0;

        var array = _samples.ToArray();
        int lastIndex = array.Length - 1;

        if (array[lastIndex].Value < threshold) return 0.0;

        int firstAboveIndex = lastIndex;
        while (firstAboveIndex >= 0 && array[firstAboveIndex].Value >= threshold)
        {
            firstAboveIndex--;
        }
        firstAboveIndex++; // First sample that started the run

        return (array[lastIndex].Time - array[firstAboveIndex].Time).TotalSeconds;
    }
}

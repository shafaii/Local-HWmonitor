using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// Status of a telemetry sensor reading after physical sanity checks.
/// </summary>
public enum TelemetryQuality
{
    Valid = 0,
    InvalidNumber = 1,          // NaN, positive/negative infinity
    PhysicalRangeViolation = 2, // Violates basic laws of physics or hardware limits
    SuspectSpike = 3,           // Sudden unnatural delta from previous known-good baseline
    Stale = 4,                  // Sensor has not updated within timeout
    Unavailable = 5             // Sensor was dropped by driver or requires elevated privileges
}

/// <summary>
/// Result of evaluating a telemetry sensor reading.
/// </summary>
public sealed record TelemetryValidationResult
{
    public required double? RawValue { get; init; }
    public required double? ValidatedValue { get; init; }
    public required TelemetryQuality Quality { get; init; }
    public string? Reason { get; init; }
    public bool IsValid => Quality == TelemetryQuality.Valid;
    public bool IsUsable => Quality == TelemetryQuality.Valid || Quality == TelemetryQuality.SuspectSpike;

    public static TelemetryValidationResult Success(double value) =>
        new() { RawValue = value, ValidatedValue = value, Quality = TelemetryQuality.Valid };

    public static TelemetryValidationResult Rejected(double? rawValue, TelemetryQuality quality, string reason) =>
        new() { RawValue = rawValue, ValidatedValue = null, Quality = quality, Reason = reason };

    public static TelemetryValidationResult Flagged(double rawValue, TelemetryQuality quality, string reason) =>
        new() { RawValue = rawValue, ValidatedValue = rawValue, Quality = quality, Reason = reason };
}

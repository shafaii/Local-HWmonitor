using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// A normalized, thread-safe hardware sensor reading.
/// </summary>
public sealed class Sensor
{
    public string SensorId { get; set; } = string.Empty;
    public string SensorName { get; set; } = string.Empty;
    public SensorType SensorType { get; set; } = SensorType.Unknown;

    public string HardwareId { get; set; } = string.Empty;
    public string HardwareName { get; set; } = string.Empty;
    public HardwareType HardwareType { get; set; } = HardwareType.Unknown;

    public double? Value { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public bool HasValue => Value.HasValue;
    public bool RequiresElevation { get; set; }

    public string FormattedValue => Value.HasValue ? $"{Value.Value:0.#} {Unit}".Trim() : "N/A";
    public string FormattedMin => Min.HasValue ? $"{Min.Value:0.#} {Unit}".Trim() : "—";
    public string FormattedMax => Max.HasValue ? $"{Max.Value:0.#} {Unit}".Trim() : "—";

    public Sensor Clone()
    {
        return new Sensor
        {
            SensorId = SensorId,
            SensorName = SensorName,
            SensorType = SensorType,
            HardwareId = HardwareId,
            HardwareName = HardwareName,
            HardwareType = HardwareType,
            Value = Value,
            Min = Min,
            Max = Max,
            Unit = Unit,
            Timestamp = Timestamp,
            RequiresElevation = RequiresElevation
        };
    }

    public override string ToString() => $"{SensorName} ({SensorType}): {FormattedValue}";
}

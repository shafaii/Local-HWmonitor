using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// Normalized metadata definition for a sensor stream associated with a hardware device.
/// </summary>
public sealed class SensorDefinitionRecord
{
    public long Id { get; set; }
    public string StableSensorIdentifier { get; set; } = string.Empty;
    public long HardwareDeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SensorType SensorType { get; set; } = SensorType.Unknown;
    public string Unit { get; set; } = string.Empty;

    // Navigation property populated for convenience
    public string? HardwareName { get; set; }
    public HardwareType HardwareType { get; set; } = HardwareType.Unknown;
}

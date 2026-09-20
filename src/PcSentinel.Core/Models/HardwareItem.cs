using System.Collections.Generic;

namespace PcSentinel.Core.Models;

/// <summary>
/// Represents a hardware component within the system topology (e.g., CPU, GPU, Motherboard, Storage).
/// </summary>
public sealed class HardwareItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HardwareType Type { get; set; } = HardwareType.Unknown;
    public string? Identifier { get; set; }

    public List<Sensor> Sensors { get; set; } = new();
    public List<HardwareItem> SubHardware { get; set; } = new();

    /// <summary>
    /// Flattens all sensors within this component and its child sub-hardware.
    /// </summary>
    public IEnumerable<Sensor> GetAllSensors()
    {
        foreach (var sensor in Sensors)
        {
            yield return sensor;
        }

        foreach (var sub in SubHardware)
        {
            foreach (var subSensor in sub.GetAllSensors())
            {
                yield return subSensor;
            }
        }
    }

    public override string ToString() => $"[{Type}] {Name} ({Sensors.Count} sensors, {SubHardware.Count} children)";
}

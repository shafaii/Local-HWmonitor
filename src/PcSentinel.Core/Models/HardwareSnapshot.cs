using System;
using System.Collections.Generic;
using System.Linq;

namespace PcSentinel.Core.Models;

/// <summary>
/// An immutable, comprehensive snapshot of all system hardware and active telemetry at a given point in time.
/// </summary>
public sealed class HardwareSnapshot
{
    public Guid SnapshotId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string MachineName { get; init; } = Environment.MachineName;
    public string OsVersion { get; init; } = Environment.OSVersion.ToString();
    public bool IsElevated { get; init; }

    public IReadOnlyList<HardwareItem> Hardware { get; init; } = Array.Empty<HardwareItem>();

    public HardwareItem? Cpu => Hardware.FirstOrDefault(h => h.Type == HardwareType.Cpu);
    public HardwareItem? Gpu => Hardware.FirstOrDefault(h => h.Type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel);
    public HardwareItem? Memory => Hardware.FirstOrDefault(h => h.Type == HardwareType.Memory);
    public IReadOnlyList<HardwareItem> StorageDrives => Hardware.Where(h => h.Type == HardwareType.Storage).ToList();
    public HardwareItem? Motherboard => Hardware.FirstOrDefault(h => h.Type == HardwareType.Motherboard);

    public IEnumerable<Sensor> AllSensors => Hardware.SelectMany(h => h.GetAllSensors());

    public Sensor? FindSensor(string hardwareId, SensorType type, string? namePrefix = null)
    {
        return AllSensors.FirstOrDefault(s =>
            s.HardwareId == hardwareId &&
            s.SensorType == type &&
            (namePrefix == null || s.SensorName.Contains(namePrefix, StringComparison.OrdinalIgnoreCase)));
    }
}

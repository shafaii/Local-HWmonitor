using PcSentinel.Core.Models;
using Lhm = LibreHardwareMonitor.Hardware;

namespace PcSentinel.Hardware.LibreHardware;

public static class LibreHardwareMapper
{
    public static HardwareType MapHardwareType(Lhm.HardwareType type) => type switch
    {
        Lhm.HardwareType.Motherboard => HardwareType.Motherboard,
        Lhm.HardwareType.SuperIO => HardwareType.SuperIO,
        Lhm.HardwareType.Cpu => HardwareType.Cpu,
        Lhm.HardwareType.Memory => HardwareType.Memory,
        Lhm.HardwareType.GpuNvidia => HardwareType.GpuNvidia,
        Lhm.HardwareType.GpuAmd => HardwareType.GpuAmd,
        Lhm.HardwareType.GpuIntel => HardwareType.GpuIntel,
        Lhm.HardwareType.Storage => HardwareType.Storage,
        Lhm.HardwareType.Network => HardwareType.Network,
        Lhm.HardwareType.Cooler => HardwareType.Cooler,
        Lhm.HardwareType.EmbeddedController => HardwareType.EmbeddedController,
        Lhm.HardwareType.Psu => HardwareType.Psu,
        Lhm.HardwareType.Battery => HardwareType.Battery,
        _ => HardwareType.Unknown
    };

    public static SensorType MapSensorType(Lhm.SensorType type) => type switch
    {
        Lhm.SensorType.Voltage => SensorType.Voltage,
        Lhm.SensorType.Current => SensorType.Current,
        Lhm.SensorType.Power => SensorType.Power,
        Lhm.SensorType.Clock => SensorType.Clock,
        Lhm.SensorType.Temperature => SensorType.Temperature,
        Lhm.SensorType.Load => SensorType.Load,
        Lhm.SensorType.Frequency => SensorType.Frequency,
        Lhm.SensorType.Fan => SensorType.Fan,
        Lhm.SensorType.Flow => SensorType.Flow,
        Lhm.SensorType.Control => SensorType.Control,
        Lhm.SensorType.Level => SensorType.Level,
        Lhm.SensorType.Factor => SensorType.Factor,
        Lhm.SensorType.Data => SensorType.Data,
        Lhm.SensorType.SmallData => SensorType.SmallData,
        Lhm.SensorType.Throughput => SensorType.Throughput,
        Lhm.SensorType.TimeSpan => SensorType.TimeSpan,
        Lhm.SensorType.Energy => SensorType.Energy,
        Lhm.SensorType.Noise => SensorType.Noise,
        Lhm.SensorType.Humidity => SensorType.Humidity,
        _ => SensorType.Unknown
    };
}

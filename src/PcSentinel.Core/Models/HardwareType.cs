namespace PcSentinel.Core.Models;

/// <summary>
/// Normalized hardware device categories.
/// </summary>
public enum HardwareType
{
    Motherboard,
    SuperIO,
    Cpu,
    Memory,
    GpuNvidia,
    GpuAmd,
    GpuIntel,
    Storage,
    Network,
    Cooler,
    EmbeddedController,
    Psu,
    Battery,
    Unknown
}

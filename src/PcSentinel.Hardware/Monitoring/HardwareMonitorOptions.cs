using System;

namespace PcSentinel.Hardware.Monitoring;

public sealed class HardwareMonitorOptions
{
    public TimeSpan DefaultPollingInterval { get; set; } = TimeSpan.FromMilliseconds(1000);
    public bool PreferLibreHardwareMonitor { get; set; } = true;
    public bool EnableCpu { get; set; } = true;
    public bool EnableGpu { get; set; } = true;
    public bool EnableMemory { get; set; } = true;
    public bool EnableStorage { get; set; } = true;
    public bool EnableMotherboard { get; set; } = true;
    public bool EnableNetwork { get; set; } = true;
}

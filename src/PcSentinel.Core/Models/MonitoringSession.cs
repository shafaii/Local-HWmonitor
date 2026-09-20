using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// Represents a bounded telemetry recording session from service start to clean stop or recovery.
/// </summary>
public sealed class MonitoringSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset StartTimeUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTimeUtc { get; set; }
    public string MachineIdentifier { get; set; } = Environment.MachineName;
    public string ApplicationVersion { get; set; } = "1.0.0";
    public int PollingIntervalMs { get; set; } = 1000;
    public bool IsInterrupted { get; set; }

    public TimeSpan? Duration => EndTimeUtc.HasValue ? EndTimeUtc.Value - StartTimeUtc : null;
    public bool IsActive => !EndTimeUtc.HasValue;
}

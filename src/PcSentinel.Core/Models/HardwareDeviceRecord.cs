using System;

namespace PcSentinel.Core.Models;

/// <summary>
/// Normalized metadata for a detected physical or logical hardware component stored in SQLite.
/// </summary>
public sealed class HardwareDeviceRecord
{
    public long Id { get; set; }
    public string StableHardwareIdentifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HardwareType HardwareType { get; set; } = HardwareType.Unknown;
    public string? ParentIdentifier { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
}

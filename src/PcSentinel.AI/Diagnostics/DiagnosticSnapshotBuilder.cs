using System;
using System.Linq;
using System.Text;
using PcSentinel.Core.Models;

namespace PcSentinel.AI.Diagnostics;

/// <summary>
/// Packages hardware telemetry into sanitized, token-efficient diagnostic prompts for Gemini AI.
/// Strips personal paths, serial numbers, and sensitive identifiers.
/// </summary>
public static class DiagnosticSnapshotBuilder
{
    public static string BuildTelemetryDigest(HardwareSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# HARDWARE TELEMETRY SNAPSHOT [{snapshot.Timestamp:yyyy-MM-dd HH:mm:ss} UTC]");
        sb.AppendLine($"OS: {snapshot.OsVersion} | Privileged Mode: {snapshot.IsElevated}");
        sb.AppendLine();

        // CPU
        if (snapshot.Cpu != null)
        {
            sb.AppendLine($"## CPU: {snapshot.Cpu.Name}");
            foreach (var s in snapshot.Cpu.Sensors.Where(s => s.Value.HasValue))
            {
                sb.AppendLine($"  - {s.SensorName}: {s.FormattedValue} (Range: {s.FormattedMin} - {s.FormattedMax})");
            }
            sb.AppendLine();
        }

        // GPU
        if (snapshot.Gpu != null)
        {
            sb.AppendLine($"## GPU: {snapshot.Gpu.Name}");
            foreach (var s in snapshot.Gpu.Sensors.Where(s => s.Value.HasValue))
            {
                sb.AppendLine($"  - {s.SensorName}: {s.FormattedValue} (Range: {s.FormattedMin} - {s.FormattedMax})");
            }
            sb.AppendLine();
        }

        // Memory
        if (snapshot.Memory != null)
        {
            sb.AppendLine($"## Memory: {snapshot.Memory.Name}");
            foreach (var s in snapshot.Memory.Sensors.Where(s => s.Value.HasValue))
            {
                sb.AppendLine($"  - {s.SensorName}: {s.FormattedValue}");
            }
            sb.AppendLine();
        }

        // Storage
        if (snapshot.StorageDrives.Count > 0)
        {
            sb.AppendLine("## Storage Devices:");
            foreach (var drive in snapshot.StorageDrives)
            {
                sb.AppendLine($"  - {drive.Name}");
                foreach (var s in drive.Sensors.Where(s => s.Value.HasValue))
                {
                    sb.AppendLine($"      * {s.SensorName}: {s.FormattedValue}");
                }
            }
            sb.AppendLine();
        }

        // System Health
        var health = SystemHealthState.Evaluate(snapshot);
        sb.AppendLine($"## System Health Assessment: {health.OverallSeverity}");
        foreach (var notice in health.HealthNotices)
        {
            sb.AppendLine($"  - [Notice] {notice}");
        }

        return sb.ToString();
    }
}

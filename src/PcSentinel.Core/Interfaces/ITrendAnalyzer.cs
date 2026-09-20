using System.Collections.Generic;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Interfaces;

/// <summary>
/// Evaluates local deterministic trends across sliding windows of telemetry readings.
/// Generates structured observations for sustained workloads, thermal anomalies, and throttling indicators.
/// </summary>
public interface ITrendAnalyzer
{
    IReadOnlyList<TelemetryObservation> AnalyzeSnapshot(HardwareSnapshot snapshot);
    IReadOnlyList<TelemetryObservation> RecentObservations { get; }
    void Reset();
}

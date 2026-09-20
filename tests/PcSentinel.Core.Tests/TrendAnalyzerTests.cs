using System;
using System.Collections.Generic;
using PcSentinel.Core.Models;
using PcSentinel.Core.Services;
using Xunit;

namespace PcSentinel.Core.Tests;

public class TrendAnalyzerTests
{
    [Fact]
    public void AnalyzeSnapshot_DetectsHighTemperature()
    {
        var analyzer = new TrendAnalyzer();
        var snapshot = CreateSnapshotWithCpuTemp(96.0, DateTimeOffset.UtcNow);

        var observations = analyzer.AnalyzeSnapshot(snapshot);

        Assert.Contains(observations, o => o.Severity == ObservationSeverity.Critical && o.Message.Contains("High CPU temperature"));
    }

    [Fact]
    public void AnalyzeSnapshot_DetectsThermalSpike()
    {
        var analyzer = new TrendAnalyzer();
        var start = DateTimeOffset.UtcNow;

        // Feed baseline temperature of 40 C over several seconds
        for (int i = 0; i < 5; i++)
        {
            analyzer.AnalyzeSnapshot(CreateSnapshotWithCpuTemp(40.0, start.AddSeconds(i)));
        }

        // Sudden jump to 65 C (delta +25 C in 1 second)
        var spikeSnapshot = CreateSnapshotWithCpuTemp(65.0, start.AddSeconds(6));
        var observations = analyzer.AnalyzeSnapshot(spikeSnapshot);

        Assert.Contains(observations, o => o.Message.Contains("thermal escalation"));
    }

    [Fact]
    public void AnalyzeSnapshot_DetectsSustainedLoad()
    {
        var analyzer = new TrendAnalyzer();
        var start = DateTimeOffset.UtcNow;

        // Feed 35 samples of 98% CPU load over 35 seconds (exceeds 20s sustained threshold)
        IReadOnlyList<TelemetryObservation> lastObs = Array.Empty<TelemetryObservation>();
        for (int i = 0; i < 35; i++)
        {
            var snap = CreateSnapshotWithCpuLoad(98.0, start.AddSeconds(i));
            lastObs = analyzer.AnalyzeSnapshot(snap);
        }

        Assert.Contains(lastObs, o => o.Message.Contains("Sustained high utilization"));
    }

    private static HardwareSnapshot CreateSnapshotWithCpuTemp(double temp, DateTimeOffset timestamp)
    {
        var cpu = new HardwareItem
        {
            Id = "/cpu/0",
            Name = "Test Processor",
            Type = HardwareType.Cpu
        };

        cpu.Sensors.Add(new Sensor
        {
            HardwareId = cpu.Id,
            HardwareName = cpu.Name,
            HardwareType = cpu.Type,
            SensorId = "/cpu/0/temperature/0",
            SensorName = "CPU Package",
            SensorType = SensorType.Temperature,
            Value = temp,
            Unit = "°C",
            Timestamp = timestamp
        });

        return new HardwareSnapshot
        {
            Timestamp = timestamp,
            Hardware = new List<HardwareItem> { cpu }
        };
    }

    private static HardwareSnapshot CreateSnapshotWithCpuLoad(double load, DateTimeOffset timestamp)
    {
        var cpu = new HardwareItem
        {
            Id = "/cpu/0",
            Name = "Test Processor",
            Type = HardwareType.Cpu
        };

        cpu.Sensors.Add(new Sensor
        {
            HardwareId = cpu.Id,
            HardwareName = cpu.Name,
            HardwareType = cpu.Type,
            SensorId = "/cpu/0/load/0",
            SensorName = "CPU Total",
            SensorType = SensorType.Load,
            Value = load,
            Unit = "%",
            Timestamp = timestamp
        });

        return new HardwareSnapshot
        {
            Timestamp = timestamp,
            Hardware = new List<HardwareItem> { cpu }
        };
    }
}

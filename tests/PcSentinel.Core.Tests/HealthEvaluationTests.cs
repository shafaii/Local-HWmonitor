using System.Collections.Generic;
using PcSentinel.Core.Models;
using Xunit;

namespace PcSentinel.Core.Tests;

public class HealthEvaluationTests
{
    [Fact]
    public void Evaluate_NominalSnapshot_ShouldReturnNormalSeverity()
    {
        var cpu = new HardwareItem { Id = "/cpu/0", Name = "Ryzen 7 7800X3D", Type = HardwareType.Cpu };
        cpu.Sensors.Add(new Sensor { SensorName = "Tdie", SensorType = SensorType.Temperature, Value = 55.0 });
        cpu.Sensors.Add(new Sensor { SensorName = "Total Load", SensorType = SensorType.Load, Value = 25.0 });

        var snapshot = new HardwareSnapshot
        {
            Hardware = new List<HardwareItem> { cpu }
        };

        var health = SystemHealthState.Evaluate(snapshot);
        Assert.Equal(HealthSeverity.Normal, health.OverallSeverity);
        Assert.False(health.IsCpuThrottling);
    }

    [Fact]
    public void Evaluate_OverheatingCpu_ShouldReturnCriticalAndThrottling()
    {
        var cpu = new HardwareItem { Id = "/cpu/0", Name = "Intel Core i9-14900K", Type = HardwareType.Cpu };
        cpu.Sensors.Add(new Sensor { SensorName = "Package", SensorType = SensorType.Temperature, Value = 98.5 });

        var snapshot = new HardwareSnapshot
        {
            Hardware = new List<HardwareItem> { cpu }
        };

        var health = SystemHealthState.Evaluate(snapshot);
        Assert.Equal(HealthSeverity.Critical, health.OverallSeverity);
        Assert.True(health.IsCpuThrottling);
        Assert.Contains(health.HealthNotices, n => n.Contains("Thermal throttling likely"));
    }
}

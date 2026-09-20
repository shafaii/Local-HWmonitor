using System;
using System.Collections.Generic;
using PcSentinel.Core.Models;
using Xunit;

namespace PcSentinel.Core.Tests;

public class HardwareSnapshotTests
{
    [Fact]
    public void HardwareSnapshot_ShouldExposeTypedComponents()
    {
        var cpu = new HardwareItem
        {
            Id = "/cpu/0",
            Name = "Intel Core i7-14700K",
            Type = HardwareType.Cpu
        };
        cpu.Sensors.Add(new Sensor
        {
            SensorId = "/cpu/0/temp/0",
            HardwareId = cpu.Id,
            SensorName = "Package",
            SensorType = SensorType.Temperature,
            Value = 55.0
        });

        var gpu = new HardwareItem
        {
            Id = "/gpu/0",
            Name = "NVIDIA GeForce RTX 4070 Ti",
            Type = HardwareType.GpuNvidia
        };

        var drive = new HardwareItem
        {
            Id = "/disk/0",
            Name = "Samsung SSD 990 PRO 2TB",
            Type = HardwareType.Storage
        };

        var snapshot = new HardwareSnapshot
        {
            Hardware = new List<HardwareItem> { cpu, gpu, drive }
        };

        Assert.NotNull(snapshot.Cpu);
        Assert.Equal("Intel Core i7-14700K", snapshot.Cpu.Name);
        Assert.NotNull(snapshot.Gpu);
        Assert.Single(snapshot.StorageDrives);
        Assert.NotNull(snapshot.FindSensor("/cpu/0", SensorType.Temperature, "Package"));
    }
}

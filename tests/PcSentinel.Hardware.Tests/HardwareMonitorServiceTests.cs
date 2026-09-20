using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;
using PcSentinel.Hardware.Fallback;
using PcSentinel.Hardware.LibreHardware;
using PcSentinel.Hardware.Monitoring;
using Xunit;

namespace PcSentinel.Hardware.Tests;

public class HardwareMonitorServiceTests
{
    private sealed class MockHardwareProvider : IHardwareProvider
    {
        public string ProviderName => "MockProvider";
        public bool IsAvailable => true;
        public bool RequiresAdministrator => false;

        public void Initialize() { }

        public IReadOnlyList<HardwareItem> PollHardware()
        {
            var cpu = new HardwareItem
            {
                Id = "/cpu/0",
                Name = "Test Processor Core i7",
                Type = HardwareType.Cpu
            };

            cpu.Sensors.Add(new Sensor
            {
                SensorId = "/cpu/0/temp/0",
                SensorName = "CPU Package",
                SensorType = SensorType.Temperature,
                Value = 58.2,
                Unit = "°C"
            });

            var mem = new HardwareItem
            {
                Id = "/ram",
                Name = "System RAM",
                Type = HardwareType.Memory
            };

            mem.Sensors.Add(new Sensor
            {
                SensorId = "/ram/load/0",
                SensorName = "Memory Used",
                SensorType = SensorType.Load,
                Value = 42.0,
                Unit = "%"
            });

            return new List<HardwareItem> { cpu, mem };
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task RefreshNowAsync_ShouldProduceValidSnapshot()
    {
        using var service = new HardwareMonitorService(
            new MockHardwareProvider(),
            new DiagnosticHardwareProvider());

        var snapshot = await service.RefreshNowAsync();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Cpu);
        Assert.NotNull(snapshot.Memory);
        Assert.Equal("Test Processor Core i7", snapshot.Cpu.Name);

        var tempSensor = snapshot.FindSensor("/cpu/0", SensorType.Temperature);
        Assert.NotNull(tempSensor);
        Assert.Equal(58.2, tempSensor!.Value);
    }

    [Fact]
    public async Task PollingLoop_ShouldFireTelemetryUpdatedEvent()
    {
        using var service = new HardwareMonitorService(
            new MockHardwareProvider(),
            new DiagnosticHardwareProvider(),
            new HardwareMonitorOptions { DefaultPollingInterval = TimeSpan.FromMilliseconds(50) });

        var tcs = new TaskCompletionSource<HardwareSnapshot>();
        service.TelemetryUpdated += (_, snap) => tcs.TrySetResult(snap);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await service.StartAsync(cts.Token);

        var receivedSnapshot = await tcs.Task;
        Assert.NotNull(receivedSnapshot);
        Assert.True(service.IsMonitoring);

        await service.StopAsync();
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public void DiagnosticHardwareProvider_DiscoversComponentsGracefully()
    {
        using var provider = new DiagnosticHardwareProvider();
        provider.Initialize();
        var hardware = provider.PollHardware();

        Assert.NotEmpty(hardware);
        Assert.Contains(hardware, h => h.Type == HardwareType.Cpu);
        Assert.Contains(hardware, h => h.Type == HardwareType.Memory);
    }

    [Fact]
    public void HardwareDiscoveryVerification_ProbesPhysicalAndFallbackHardware()
    {
        using var lhm = new LibreHardwareMonitorProvider();
        lhm.Initialize();
        var lhmItems = lhm.PollHardware();

        Console.WriteLine($"[HARDWARE PROBE] LibreHardwareMonitor IsAvailable: {lhm.IsAvailable}, Item Count: {lhmItems.Count}");
        foreach (var item in lhmItems)
        {
            Console.WriteLine($"  LHM Hardware: {item.Name} ({item.Type}) Sensors: {item.Sensors.Count}");
            foreach (var s in item.Sensors)
            {
                Console.WriteLine($"    Sensor: {s.SensorName} [{s.SensorType}] = {s.Value} {s.Unit} (Min: {s.Min}, Max: {s.Max})");
            }
        }

        using var diag = new DiagnosticHardwareProvider();
        diag.Initialize();
        var diagItems = diag.PollHardware();
        Console.WriteLine($"[HARDWARE PROBE] DiagnosticFallback Item Count: {diagItems.Count}");
        foreach (var item in diagItems)
        {
            Console.WriteLine($"  Diag Hardware: {item.Name} ({item.Type}) Sensors: {item.Sensors.Count}");
            foreach (var s in item.Sensors)
            {
                Console.WriteLine($"    Sensor: {s.SensorName} [{s.SensorType}] = {s.Value} {s.Unit} (Min: {s.Min}, Max: {s.Max})");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PcSentinel.Core.Models;
using PcSentinel.Infrastructure.Persistence;
using Xunit;

namespace PcSentinel.Infrastructure.Tests;

public class ChannelTelemetryWriterTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteTelemetryRepository _repository;

    public ChannelTelemetryWriterTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"pcsentinel_writer_{Guid.NewGuid():N}.db");
        _repository = new SqliteTelemetryRepository(_testDbPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task ChannelTelemetryWriter_WritesBatchesToRepository()
    {
        await _repository.InitializeAsync();
        var session = await _repository.StartSessionAsync(1000);
        var writer = new ChannelTelemetryWriter(_repository);

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
            SensorId = "/cpu/0/temp/0",
            SensorName = "Core Temp",
            SensorType = SensorType.Temperature,
            Value = 48.5,
            Unit = "°C",
            Timestamp = DateTimeOffset.UtcNow
        });

        var snapshot = new HardwareSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Hardware = new List<HardwareItem> { cpu }
        };

        // Enqueue snapshot
        bool enqueued = writer.EnqueueSnapshot(snapshot, session.Id);
        Assert.True(enqueued);

        // Wait a short duration and flush
        await Task.Delay(200);
        await writer.FlushAsync();

        var stats = await _repository.GetStorageStatisticsAsync();
        Assert.True(stats.TotalReadings >= 1);

        await writer.DisposeAsync();
    }
}

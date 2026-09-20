using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PcSentinel.Core.Models;
using PcSentinel.Infrastructure.Persistence;
using Xunit;

namespace PcSentinel.Infrastructure.Tests;

public class HistoricalQueryServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteTelemetryRepository _repository;
    private readonly HistoricalQueryService _service;

    public HistoricalQueryServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"pcsentinel_query_{Guid.NewGuid():N}.db");
        _repository = new SqliteTelemetryRepository(_testDbPath);
        _service = new HistoricalQueryService(_repository);
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
    public async Task QuerySensorDataAsync_ReturnsAggregatedResults()
    {
        await _repository.InitializeAsync();
        var session = await _repository.StartSessionAsync(1000);

        var device = await _repository.GetOrCreateDeviceAsync("/cpu/0", "Test CPU", HardwareType.Cpu);
        var sensor = await _repository.GetOrCreateSensorDefinitionAsync("/cpu/0/temp/0", device.Id, "Core Temp", SensorType.Temperature, "°C");

        var now = DateTimeOffset.UtcNow;

        // Seed some readings
        var readings = new List<SensorReadingRecord>
        {
            new(sensor.Id, now.AddSeconds(-120), 55.0, session.Id),
            new(sensor.Id, now.AddSeconds(-60), 58.0, session.Id),
            new(sensor.Id, now.AddSeconds(-10), 62.0, session.Id)
        };

        await _repository.InsertReadingsBatchAsync(readings);

        var result = await _service.QuerySensorDataAsync(sensor.Id, TimeRange.FiveMinutes);
        Assert.NotEmpty(result);
    }
}

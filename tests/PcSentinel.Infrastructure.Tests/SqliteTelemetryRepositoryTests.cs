using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PcSentinel.Core.Models;
using PcSentinel.Infrastructure.Persistence;
using Xunit;

namespace PcSentinel.Infrastructure.Tests;

public class SqliteTelemetryRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteTelemetryRepository _repository;

    public SqliteTelemetryRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"pcsentinel_test_{Guid.NewGuid():N}.db");
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
    public async Task InitializeAsync_CreatesTablesSuccessfully()
    {
        await _repository.InitializeAsync();
        var stats = await _repository.GetStorageStatisticsAsync();
        Assert.Equal(0, stats.TotalReadings);
        Assert.True(File.Exists(_testDbPath));
    }

    [Fact]
    public async Task InsertReadingsBatchAsync_PersistsAndQueriesAccurately()
    {
        await _repository.InitializeAsync();
        var session = await _repository.StartSessionAsync(1000);

        var device = await _repository.GetOrCreateDeviceAsync("/cpu/0", "Intel Core i7", HardwareType.Cpu);
        var sensor = await _repository.GetOrCreateSensorDefinitionAsync("/cpu/0/temp/0", device.Id, "Package Temp", SensorType.Temperature, "°C");

        var now = DateTimeOffset.UtcNow;
        var readings = new List<SensorReadingRecord>
        {
            new(sensor.Id, now.AddSeconds(-20), 50.0, session.Id),
            new(sensor.Id, now.AddSeconds(-10), 60.0, session.Id),
            new(sensor.Id, now, 70.0, session.Id)
        };

        await _repository.InsertReadingsBatchAsync(readings);

        var retrieved = await _repository.QuerySensorHistoryAsync(
            sensor.Id,
            now.AddSeconds(-30),
            now.AddSeconds(10),
            TimeSpan.FromSeconds(5));

        Assert.NotEmpty(retrieved);
        Assert.Equal(50.0, retrieved[0].Min);
    }

    [Fact]
    public async Task QuerySensorHistoryAsync_AggregatesCorrectly()
    {
        await _repository.InitializeAsync();
        var session = await _repository.StartSessionAsync(1000);

        var device = await _repository.GetOrCreateDeviceAsync("/cpu/0", "Intel Core i7", HardwareType.Cpu);
        var sensor = await _repository.GetOrCreateSensorDefinitionAsync("/cpu/0/temp/1", device.Id, "Core 0", SensorType.Temperature, "°C");

        long unixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long alignedSec = (unixSec / 60) * 60;
        var baseTime = DateTimeOffset.FromUnixTimeSeconds(alignedSec);

        var readings = new List<SensorReadingRecord>
        {
            new(sensor.Id, baseTime.AddSeconds(5), 40.0, session.Id),
            new(sensor.Id, baseTime.AddSeconds(15), 50.0, session.Id),
            new(sensor.Id, baseTime.AddSeconds(25), 60.0, session.Id)
        };

        await _repository.InsertReadingsBatchAsync(readings);

        var aggregated = await _repository.QuerySensorHistoryAsync(
            sensor.Id,
            baseTime,
            baseTime.AddSeconds(60),
            TimeSpan.FromSeconds(60));

        Assert.Single(aggregated);
        Assert.Equal(40.0, aggregated[0].Min);
        Assert.Equal(60.0, aggregated[0].Max);
        Assert.Equal(50.0, aggregated[0].Average);
        Assert.Equal(3, aggregated[0].SampleCount);
    }

    [Fact]
    public async Task PurgeExpiredReadingsAsync_DeletesOldRecords()
    {
        await _repository.InitializeAsync();
        var session = await _repository.StartSessionAsync(1000);

        var device = await _repository.GetOrCreateDeviceAsync("/cpu/0", "Intel Core i7", HardwareType.Cpu);
        var sensor = await _repository.GetOrCreateSensorDefinitionAsync("/cpu/0/temp/2", device.Id, "Core 1", SensorType.Temperature, "°C");

        var now = DateTimeOffset.UtcNow;
        var readings = new List<SensorReadingRecord>
        {
            new(sensor.Id, now.AddDays(-10), 45.0, session.Id), // Old: should be purged
            new(sensor.Id, now.AddDays(-8), 46.0, session.Id),  // Old: should be purged
            new(sensor.Id, now.AddMinutes(-5), 55.0, session.Id) // Recent: should be kept
        };

        await _repository.InsertReadingsBatchAsync(readings);

        int purged = await _repository.PurgeExpiredReadingsAsync(TimeSpan.FromDays(7));
        Assert.Equal(2, purged);

        var remaining = await _repository.QuerySensorHistoryAsync(
            sensor.Id,
            now.AddDays(-15),
            now,
            TimeSpan.FromMinutes(1));

        Assert.Single(remaining);
        Assert.Equal(55.0, remaining[0].Average);
    }
}

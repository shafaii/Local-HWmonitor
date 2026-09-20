using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.Infrastructure.Persistence;

/// <summary>
/// Service providing high-level historical sensor queries with automated downsampling.
/// </summary>
public sealed class HistoricalQueryService : IHistoricalQueryService
{
    private readonly ITelemetryRepository _repository;

    public HistoricalQueryService(ITelemetryRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<HardwareDeviceRecord>> GetAvailableHardwareAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllDevicesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<SensorDefinitionRecord>> GetSensorsForHardwareAsync(long hardwareDeviceId, CancellationToken cancellationToken = default)
    {
        return _repository.GetSensorsForDeviceAsync(hardwareDeviceId, cancellationToken);
    }

    public Task<IReadOnlyList<AggregatedReading>> QuerySensorDataAsync(
        long sensorDefinitionId,
        TimeRange timeRange,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var duration = TimeRangeHelper.ToTimeSpan(timeRange);
        var startTime = now.Subtract(duration);
        var bucketSize = TimeRangeHelper.GetRecommendedBucketSize(timeRange);

        return _repository.QuerySensorHistoryAsync(sensorDefinitionId, startTime, now, bucketSize, cancellationToken);
    }

    public Task<IReadOnlyList<AggregatedReading>> QuerySensorCustomRangeAsync(
        long sensorDefinitionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeSpan bucketSize,
        CancellationToken cancellationToken = default)
    {
        return _repository.QuerySensorHistoryAsync(sensorDefinitionId, startTime, endTime, bucketSize, cancellationToken);
    }
}

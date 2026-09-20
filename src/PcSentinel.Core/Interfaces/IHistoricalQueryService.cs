using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Interfaces;

/// <summary>
/// High-level querying service for historical telemetry data and adaptive downsampling.
/// </summary>
public interface IHistoricalQueryService
{
    Task<IReadOnlyList<HardwareDeviceRecord>> GetAvailableHardwareAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SensorDefinitionRecord>> GetSensorsForHardwareAsync(long hardwareDeviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AggregatedReading>> QuerySensorDataAsync(
        long sensorDefinitionId,
        TimeRange timeRange,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AggregatedReading>> QuerySensorCustomRangeAsync(
        long sensorDefinitionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeSpan bucketSize,
        CancellationToken cancellationToken = default);
}

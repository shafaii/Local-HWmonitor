using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Interfaces;

/// <summary>
/// Core contract for persistent storage of telemetry sessions, hardware metadata, and sensor readings.
/// Completely decoupled from SQLite or any specific storage technology.
/// </summary>
public interface ITelemetryRepository : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    // Sessions
    Task<MonitoringSession> StartSessionAsync(int pollingIntervalMs, CancellationToken cancellationToken = default);
    Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonitoringSession>> GetSessionsAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<int> RecoverInterruptedSessionsAsync(CancellationToken cancellationToken = default);

    // Hardware & Sensor Definitions
    Task<HardwareDeviceRecord> GetOrCreateDeviceAsync(string stableHardwareId, string name, HardwareType type, string? parentId = null, CancellationToken cancellationToken = default);
    Task<SensorDefinitionRecord> GetOrCreateSensorDefinitionAsync(string stableSensorId, long hardwareDeviceId, string name, SensorType type, string unit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HardwareDeviceRecord>> GetAllDevicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SensorDefinitionRecord>> GetSensorsForDeviceAsync(long hardwareDeviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SensorDefinitionRecord>> GetAllSensorsAsync(CancellationToken cancellationToken = default);

    // Telemetry Samples
    Task InsertReadingsBatchAsync(IReadOnlyList<SensorReadingRecord> readings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AggregatedReading>> QuerySensorHistoryAsync(long sensorDefinitionId, DateTimeOffset startTime, DateTimeOffset endTime, TimeSpan bucketSize, CancellationToken cancellationToken = default);
    Task<int> PurgeExpiredReadingsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

    // Database stats
    Task<(long TotalReadings, long DatabaseSizeBytes)> GetStorageStatisticsAsync(CancellationToken cancellationToken = default);
}

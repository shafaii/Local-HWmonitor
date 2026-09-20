using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.Infrastructure.Persistence;

/// <summary>
/// High-performance SQLite-backed repository for historical telemetry storage,
/// monitoring session tracking, hardware metadata, and aggregated downsampling queries.
/// </summary>
public sealed class SqliteTelemetryRepository : ITelemetryRepository
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly ILogger<SqliteTelemetryRepository>? _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _isInitialized;
    private bool _isDisposed;

    // In-memory lookup caches to prevent repeated DB round-trips for metadata
    private readonly ConcurrentDictionary<string, HardwareDeviceRecord> _deviceCache = new();
    private readonly ConcurrentDictionary<string, SensorDefinitionRecord> _sensorCache = new();

    public string DatabasePath => _databasePath;

    public SqliteTelemetryRepository(
        string? databasePath = null,
        ILogger<SqliteTelemetryRepository>? logger = null)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appData, "PcSentinel");
            Directory.CreateDirectory(appFolder);
            _databasePath = Path.Combine(appFolder, "pcsentinel_telemetry.db");
            _connectionString = $"Data Source={_databasePath};Mode=ReadWriteCreate;";
        }
        else if (databasePath == ":memory:")
        {
            _databasePath = ":memory:";
            _connectionString = "Data Source=:memory:;Mode=Memory;Cache=Shared;";
        }
        else
        {
            _databasePath = databasePath;
            string? dir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _connectionString = $"Data Source={_databasePath};Mode=ReadWriteCreate;";
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            _logger?.LogInformation("Initializing SQLite telemetry database at {Path}...", _databasePath);

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Configure SQLite pragmas for maximum write throughput and concurrent readers
            using (var pragmaCmd = connection.CreateCommand())
            {
                pragmaCmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA foreign_keys = ON;
                    PRAGMA temp_store = MEMORY;
                ";
                await pragmaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Create schema tables and indexes
            using (var schemaCmd = connection.CreateCommand())
            {
                schemaCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS monitoring_session (
                        id TEXT PRIMARY KEY,
                        start_time_utc INTEGER NOT NULL,
                        end_time_utc INTEGER NULL,
                        machine_identifier TEXT NOT NULL,
                        application_version TEXT NOT NULL,
                        polling_interval_ms INTEGER NOT NULL,
                        is_interrupted INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS hardware_device (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        stable_hardware_identifier TEXT NOT NULL UNIQUE,
                        name TEXT NOT NULL,
                        hardware_type INTEGER NOT NULL,
                        parent_identifier TEXT NULL,
                        first_seen_utc INTEGER NOT NULL,
                        last_seen_utc INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS sensor_definition (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        stable_sensor_identifier TEXT NOT NULL UNIQUE,
                        hardware_device_id INTEGER NOT NULL REFERENCES hardware_device(id) ON DELETE CASCADE,
                        name TEXT NOT NULL,
                        sensor_type INTEGER NOT NULL,
                        unit TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS sensor_reading (
                        sensor_definition_id INTEGER NOT NULL REFERENCES sensor_definition(id) ON DELETE CASCADE,
                        timestamp_utc INTEGER NOT NULL,
                        value REAL NOT NULL,
                        session_id TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS ix_reading_sensor_time ON sensor_reading (sensor_definition_id, timestamp_utc);
                    CREATE INDEX IF NOT EXISTS ix_reading_session_time ON sensor_reading (session_id, timestamp_utc);
                    CREATE INDEX IF NOT EXISTS ix_reading_time ON sensor_reading (timestamp_utc);
                ";
                await schemaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _isInitialized = true;
            _logger?.LogInformation("SQLite telemetry database schema initialized successfully.");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    #region Monitoring Sessions

    public async Task<MonitoringSession> StartSessionAsync(int pollingIntervalMs, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var session = new MonitoringSession
        {
            Id = Guid.NewGuid().ToString("N"),
            StartTimeUtc = DateTimeOffset.UtcNow,
            MachineIdentifier = Environment.MachineName,
            ApplicationVersion = "1.0.0",
            PollingIntervalMs = pollingIntervalMs,
            IsInterrupted = false
        };

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO monitoring_session (id, start_time_utc, machine_identifier, application_version, polling_interval_ms, is_interrupted)
                VALUES (@id, @start, @machine, @version, @interval, 0);
            ";
            cmd.Parameters.AddWithValue("@id", session.Id);
            cmd.Parameters.AddWithValue("@start", session.StartTimeUtc.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("@machine", session.MachineIdentifier);
            cmd.Parameters.AddWithValue("@version", session.ApplicationVersion);
            cmd.Parameters.AddWithValue("@interval", session.PollingIntervalMs);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("Started monitoring session {SessionId} (Interval: {Interval}ms)", session.Id, pollingIntervalMs);
            return session;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE monitoring_session
                SET end_time_utc = @end
                WHERE id = @id AND end_time_utc IS NULL;
            ";
            cmd.Parameters.AddWithValue("@end", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("@id", sessionId);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("Closed monitoring session {SessionId} cleanly.", sessionId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<MonitoringSession>> GetSessionsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, start_time_utc, end_time_utc, machine_identifier, application_version, polling_interval_ms, is_interrupted
            FROM monitoring_session
            ORDER BY start_time_utc DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);

        var list = new List<MonitoringSession>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new MonitoringSession
            {
                Id = reader.GetString(0),
                StartTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                EndTimeUtc = reader.IsDBNull(2) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)),
                MachineIdentifier = reader.GetString(3),
                ApplicationVersion = reader.GetString(4),
                PollingIntervalMs = reader.GetInt32(5),
                IsInterrupted = reader.GetInt32(6) == 1
            });
        }

        return list;
    }

    public async Task<int> RecoverInterruptedSessionsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE monitoring_session
                SET is_interrupted = 1,
                    end_time_utc = COALESCE(
                        (SELECT MAX(timestamp_utc) FROM sensor_reading WHERE session_id = monitoring_session.id),
                        start_time_utc
                    )
                WHERE end_time_utc IS NULL;
            ";

            int affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affected > 0)
            {
                _logger?.LogWarning("Recovered {Count} interrupted monitoring session(s) from previous unclean shutdown.", affected);
            }
            return affected;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    #endregion

    #region Hardware & Sensor Metadata

    public async Task<HardwareDeviceRecord> GetOrCreateDeviceAsync(
        string stableHardwareId,
        string name,
        HardwareType type,
        string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        if (_deviceCache.TryGetValue(stableHardwareId, out var cached))
        {
            return cached;
        }

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_deviceCache.TryGetValue(stableHardwareId, out cached))
            {
                return cached;
            }

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var upsertCmd = connection.CreateCommand();
            upsertCmd.CommandText = @"
                INSERT INTO hardware_device (stable_hardware_identifier, name, hardware_type, parent_identifier, first_seen_utc, last_seen_utc)
                VALUES (@stableId, @name, @type, @parentId, @now, @now)
                ON CONFLICT(stable_hardware_identifier) DO UPDATE SET
                    name = @name,
                    last_seen_utc = @now;
                
                SELECT id, stable_hardware_identifier, name, hardware_type, parent_identifier, first_seen_utc, last_seen_utc
                FROM hardware_device
                WHERE stable_hardware_identifier = @stableId;
            ";
            upsertCmd.Parameters.AddWithValue("@stableId", stableHardwareId);
            upsertCmd.Parameters.AddWithValue("@name", name);
            upsertCmd.Parameters.AddWithValue("@type", (int)type);
            upsertCmd.Parameters.AddWithValue("@parentId", (object?)parentId ?? DBNull.Value);
            upsertCmd.Parameters.AddWithValue("@now", now);

            using var reader = await upsertCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var record = new HardwareDeviceRecord
                {
                    Id = reader.GetInt64(0),
                    StableHardwareIdentifier = reader.GetString(1),
                    Name = reader.GetString(2),
                    HardwareType = (HardwareType)reader.GetInt32(3),
                    ParentIdentifier = reader.IsDBNull(4) ? null : reader.GetString(4),
                    FirstSeenUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                    LastSeenUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6))
                };

                _deviceCache[stableHardwareId] = record;
                return record;
            }

            throw new InvalidOperationException($"Failed to query hardware device after upsert: {stableHardwareId}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<SensorDefinitionRecord> GetOrCreateSensorDefinitionAsync(
        string stableSensorId,
        long hardwareDeviceId,
        string name,
        SensorType type,
        string unit,
        CancellationToken cancellationToken = default)
    {
        if (_sensorCache.TryGetValue(stableSensorId, out var cached))
        {
            return cached;
        }

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sensorCache.TryGetValue(stableSensorId, out cached))
            {
                return cached;
            }

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var upsertCmd = connection.CreateCommand();
            upsertCmd.CommandText = @"
                INSERT INTO sensor_definition (stable_sensor_identifier, hardware_device_id, name, sensor_type, unit)
                VALUES (@stableId, @devId, @name, @type, @unit)
                ON CONFLICT(stable_sensor_identifier) DO UPDATE SET
                    name = @name,
                    unit = @unit;

                SELECT s.id, s.stable_sensor_identifier, s.hardware_device_id, s.name, s.sensor_type, s.unit, h.name, h.hardware_type
                FROM sensor_definition s
                JOIN hardware_device h ON s.hardware_device_id = h.id
                WHERE s.stable_sensor_identifier = @stableId;
            ";
            upsertCmd.Parameters.AddWithValue("@stableId", stableSensorId);
            upsertCmd.Parameters.AddWithValue("@devId", hardwareDeviceId);
            upsertCmd.Parameters.AddWithValue("@name", name);
            upsertCmd.Parameters.AddWithValue("@type", (int)type);
            upsertCmd.Parameters.AddWithValue("@unit", unit);

            using var reader = await upsertCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var record = new SensorDefinitionRecord
                {
                    Id = reader.GetInt64(0),
                    StableSensorIdentifier = reader.GetString(1),
                    HardwareDeviceId = reader.GetInt64(2),
                    Name = reader.GetString(3),
                    SensorType = (SensorType)reader.GetInt32(4),
                    Unit = reader.GetString(5),
                    HardwareName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    HardwareType = reader.IsDBNull(7) ? HardwareType.Unknown : (HardwareType)reader.GetInt32(7)
                };

                _sensorCache[stableSensorId] = record;
                return record;
            }

            throw new InvalidOperationException($"Failed to query sensor definition after upsert: {stableSensorId}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<HardwareDeviceRecord>> GetAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, stable_hardware_identifier, name, hardware_type, parent_identifier, first_seen_utc, last_seen_utc
            FROM hardware_device
            ORDER BY hardware_type ASC, name ASC;
        ";

        var list = new List<HardwareDeviceRecord>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new HardwareDeviceRecord
            {
                Id = reader.GetInt64(0),
                StableHardwareIdentifier = reader.GetString(1),
                Name = reader.GetString(2),
                HardwareType = (HardwareType)reader.GetInt32(3),
                ParentIdentifier = reader.IsDBNull(4) ? null : reader.GetString(4),
                FirstSeenUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                LastSeenUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6))
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<SensorDefinitionRecord>> GetSensorsForDeviceAsync(long hardwareDeviceId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.id, s.stable_sensor_identifier, s.hardware_device_id, s.name, s.sensor_type, s.unit, h.name, h.hardware_type
            FROM sensor_definition s
            JOIN hardware_device h ON s.hardware_device_id = h.id
            WHERE s.hardware_device_id = @devId
            ORDER BY s.sensor_type ASC, s.name ASC;
        ";
        cmd.Parameters.AddWithValue("@devId", hardwareDeviceId);

        var list = new List<SensorDefinitionRecord>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new SensorDefinitionRecord
            {
                Id = reader.GetInt64(0),
                StableSensorIdentifier = reader.GetString(1),
                HardwareDeviceId = reader.GetInt64(2),
                Name = reader.GetString(3),
                SensorType = (SensorType)reader.GetInt32(4),
                Unit = reader.GetString(5),
                HardwareName = reader.GetString(6),
                HardwareType = (HardwareType)reader.GetInt32(7)
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<SensorDefinitionRecord>> GetAllSensorsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.id, s.stable_sensor_identifier, s.hardware_device_id, s.name, s.sensor_type, s.unit, h.name, h.hardware_type
            FROM sensor_definition s
            JOIN hardware_device h ON s.hardware_device_id = h.id
            ORDER BY h.hardware_type ASC, s.sensor_type ASC, s.name ASC;
        ";

        var list = new List<SensorDefinitionRecord>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new SensorDefinitionRecord
            {
                Id = reader.GetInt64(0),
                StableSensorIdentifier = reader.GetString(1),
                HardwareDeviceId = reader.GetInt64(2),
                Name = reader.GetString(3),
                SensorType = (SensorType)reader.GetInt32(4),
                Unit = reader.GetString(5),
                HardwareName = reader.GetString(6),
                HardwareType = (HardwareType)reader.GetInt32(7)
            });
        }
        return list;
    }

    #endregion

    #region Telemetry Samples & Batched Writes

    public async Task InsertReadingsBatchAsync(IReadOnlyList<SensorReadingRecord> readings, CancellationToken cancellationToken = default)
    {
        if (readings.Count == 0) return;

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var transaction = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;

            // Parameterized multi-row insert or prepared statement loop
            cmd.CommandText = @"
                INSERT INTO sensor_reading (sensor_definition_id, timestamp_utc, value, session_id)
                VALUES (@defId, @time, @val, @sess);
            ";

            var pDefId = cmd.Parameters.Add("@defId", SqliteType.Integer);
            var pTime = cmd.Parameters.Add("@time", SqliteType.Integer);
            var pVal = cmd.Parameters.Add("@val", SqliteType.Real);
            var pSess = cmd.Parameters.Add("@sess", SqliteType.Text);

            foreach (var r in readings)
            {
                pDefId.Value = r.SensorDefinitionId;
                pTime.Value = r.TimestampUtc.ToUnixTimeMilliseconds();
                pVal.Value = r.Value;
                pSess.Value = r.SessionId;

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<AggregatedReading>> QuerySensorHistoryAsync(
        long sensorDefinitionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeSpan bucketSize,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        long startMs = startTime.ToUnixTimeMilliseconds();
        long endMs = endTime.ToUnixTimeMilliseconds();
        long bucketMs = (long)bucketSize.TotalMilliseconds;

        using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();

        if (bucketMs <= 0)
        {
            // Raw sensor samples (near 1-sec resolution for 5-min range)
            cmd.CommandText = @"
                SELECT timestamp_utc, value, value, value, 1
                FROM sensor_reading
                WHERE sensor_definition_id = @defId
                  AND timestamp_utc >= @start
                  AND timestamp_utc <= @end
                ORDER BY timestamp_utc ASC;
            ";
        }
        else
        {
            // Statistical downsampling aggregation per bucket interval
            cmd.CommandText = @"
                SELECT 
                    (timestamp_utc / @bucketMs) * @bucketMs AS bucket_time,
                    AVG(value) AS avg_val,
                    MIN(value) AS min_val,
                    MAX(value) AS max_val,
                    COUNT(*) AS sample_count
                FROM sensor_reading
                WHERE sensor_definition_id = @defId
                  AND timestamp_utc >= @start
                  AND timestamp_utc <= @end
                GROUP BY bucket_time
                ORDER BY bucket_time ASC;
            ";
            cmd.Parameters.AddWithValue("@bucketMs", bucketMs);
        }

        cmd.Parameters.AddWithValue("@defId", sensorDefinitionId);
        cmd.Parameters.AddWithValue("@start", startMs);
        cmd.Parameters.AddWithValue("@end", endMs);

        var results = new List<AggregatedReading>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new AggregatedReading
            {
                TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)),
                Average = Math.Round(reader.GetDouble(1), 2),
                Min = Math.Round(reader.GetDouble(2), 2),
                Max = Math.Round(reader.GetDouble(3), 2),
                SampleCount = reader.GetInt32(4)
            });
        }

        return results;
    }

    public async Task<int> PurgeExpiredReadingsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        long cutoffMs = DateTimeOffset.UtcNow.Subtract(retentionPeriod).ToUnixTimeMilliseconds();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM sensor_reading
                WHERE timestamp_utc < @cutoff;
            ";
            cmd.Parameters.AddWithValue("@cutoff", cutoffMs);

            int deleted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (deleted > 0)
            {
                _logger?.LogInformation("Historical retention purge completed: {DeletedCount} expired sensor readings deleted (Cutoff: {Cutoff})",
                    deleted, DateTimeOffset.FromUnixTimeMilliseconds(cutoffMs));
            }
            return deleted;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<(long TotalReadings, long DatabaseSizeBytes)> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        long count = 0;
        using (var connection = CreateConnection())
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sensor_reading;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result != null && result != DBNull.Value)
            {
                count = Convert.ToInt64(result);
            }
        }

        long sizeBytes = 0;
        if (_databasePath != ":memory:" && File.Exists(_databasePath))
        {
            try
            {
                sizeBytes = new FileInfo(_databasePath).Length;
            }
            catch
            {
                // Ignore file access locks during active WAL checkpoints
            }
        }

        return (count, sizeBytes);
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _writeLock.Dispose();
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;
using PcSentinel.Core.Services;

namespace PcSentinel.Infrastructure.Persistence;

/// <summary>
/// Asynchronous producer/consumer pipeline using bounded System.Threading.Channels.
/// Decouples hardware sampling from SQLite transactions to guarantee zero UI thread blocking.
/// </summary>
public sealed class ChannelTelemetryWriter : ITelemetryWriter
{
    private readonly ITelemetryRepository _repository;
    private readonly ILogger<ChannelTelemetryWriter>? _logger;
    private readonly Channel<SnapshotWriteItem> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumerTask;
    private bool _isDisposed;

    private long _totalReadingsWritten;

    // Cache stable sensor ID -> definition ID to avoid DB lookups during hot write loop
    private readonly ConcurrentDictionary<string, long> _sensorDefinitionIdCache = new();
    private readonly ConcurrentDictionary<string, long> _hardwareDeviceIdCache = new();

    public int PendingQueueCount => _channel.Reader.Count;
    public long TotalReadingsWritten => Interlocked.Read(ref _totalReadingsWritten);

    private sealed record SnapshotWriteItem(HardwareSnapshot Snapshot, string SessionId);

    public ChannelTelemetryWriter(
        ITelemetryRepository repository,
        int queueCapacity = 1000,
        ILogger<ChannelTelemetryWriter>? logger = null)
    {
        _repository = repository;
        _logger = logger;

        var channelOptions = new BoundedChannelOptions(Math.Max(100, queueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest, // Protect memory from infinite queue growth
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<SnapshotWriteItem>(channelOptions);
        _consumerTask = Task.Run(() => ConsumerLoopAsync(_cts.Token));
    }

    public bool EnqueueSnapshot(HardwareSnapshot snapshot, string sessionId)
    {
        if (_isDisposed || _cts.IsCancellationRequested) return false;

        var item = new SnapshotWriteItem(snapshot, sessionId);
        bool written = _channel.Writer.TryWrite(item);

        if (!written)
        {
            _logger?.LogWarning("Persistence channel queue is full; dropped oldest snapshot to avoid blocking telemetry loop.");
        }

        return written;
    }

    private async Task ConsumerLoopAsync(CancellationToken ct)
    {
        _logger?.LogInformation("Telemetry persistence consumer loop started.");

        var batchBuffer = new List<SensorReadingRecord>(1024);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Wait for at least one item
                if (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    batchBuffer.Clear();

                    // Drain all available snapshots currently in channel up to batch size limit
                    while (_channel.Reader.TryRead(out var item))
                    {
                        await ExtractReadingsFromSnapshotAsync(item.Snapshot, item.SessionId, batchBuffer, ct).ConfigureAwait(false);
                        if (batchBuffer.Count >= 2000) break;
                    }

                    if (batchBuffer.Count > 0)
                    {
                        await _repository.InsertReadingsBatchAsync(batchBuffer, ct).ConfigureAwait(false);
                        Interlocked.Add(ref _totalReadingsWritten, batchBuffer.Count);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected exception during batched telemetry persistence: {Message}", ex.Message);
                await Task.Delay(250, ct).ConfigureAwait(false);
            }
        }

        // Drain remainder upon shutdown
        try
        {
            batchBuffer.Clear();
            while (_channel.Reader.TryRead(out var item))
            {
                await ExtractReadingsFromSnapshotAsync(item.Snapshot, item.SessionId, batchBuffer, CancellationToken.None).ConfigureAwait(false);
            }

            if (batchBuffer.Count > 0)
            {
                await _repository.InsertReadingsBatchAsync(batchBuffer, CancellationToken.None).ConfigureAwait(false);
                Interlocked.Add(ref _totalReadingsWritten, batchBuffer.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error draining remaining telemetry readings on shutdown: {Message}", ex.Message);
        }

        _logger?.LogInformation("Telemetry persistence consumer loop terminated cleanly. Total written: {Total}", TotalReadingsWritten);
    }

    private async Task ExtractReadingsFromSnapshotAsync(
        HardwareSnapshot snapshot,
        string sessionId,
        List<SensorReadingRecord> destination,
        CancellationToken ct)
    {
        var timestamp = snapshot.Timestamp;

        foreach (var hw in snapshot.Hardware)
        {
            await ProcessHardwareRecursiveAsync(hw, timestamp, sessionId, destination, ct).ConfigureAwait(false);
        }
    }

    private async Task ProcessHardwareRecursiveAsync(
        HardwareItem hw,
        DateTimeOffset timestamp,
        string sessionId,
        List<SensorReadingRecord> destination,
        CancellationToken ct)
    {
        string stableHardwareId = StableIdentifier.CreateHardwareId(hw.Type, hw.Name, hw.Identifier);

        if (!_hardwareDeviceIdCache.TryGetValue(stableHardwareId, out long hardwareDeviceId))
        {
            var deviceRecord = await _repository.GetOrCreateDeviceAsync(
                stableHardwareId,
                hw.Name,
                hw.Type,
                null,
                ct).ConfigureAwait(false);

            hardwareDeviceId = deviceRecord.Id;
            _hardwareDeviceIdCache[stableHardwareId] = hardwareDeviceId;
        }

        foreach (var sensor in hw.Sensors)
        {
            if (!sensor.Value.HasValue) continue;

            // Telemetry Quality validation: reject NaNs, infinities, physical impossibilities
            var validation = TelemetryValidator.Validate(sensor.SensorType, sensor.Value);
            if (!validation.IsUsable || !validation.ValidatedValue.HasValue)
            {
                continue;
            }

            string stableSensorId = StableIdentifier.CreateSensorId(stableHardwareId, sensor.SensorType, sensor.SensorName);

            if (!_sensorDefinitionIdCache.TryGetValue(stableSensorId, out long sensorDefId))
            {
                var sensorRecord = await _repository.GetOrCreateSensorDefinitionAsync(
                    stableSensorId,
                    hardwareDeviceId,
                    sensor.SensorName,
                    sensor.SensorType,
                    sensor.Unit,
                    ct).ConfigureAwait(false);

                sensorDefId = sensorRecord.Id;
                _sensorDefinitionIdCache[stableSensorId] = sensorDefId;
            }

            destination.Add(new SensorReadingRecord(sensorDefId, timestamp, validation.ValidatedValue.Value, sessionId));
        }

        foreach (var sub in hw.SubHardware)
        {
            await ProcessHardwareRecursiveAsync(sub, timestamp, sessionId, destination, ct).ConfigureAwait(false);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Wait until queue is completely empty
        while (_channel.Reader.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _channel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            await _consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error while waiting for consumer task completion during disposal: {Message}", ex.Message);
        }

        _cts.Dispose();
    }
}

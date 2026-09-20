using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;
using PcSentinel.Hardware.Fallback;
using PcSentinel.Hardware.LibreHardware;

namespace PcSentinel.Hardware.Monitoring;

/// <summary>
/// Thread-safe, non-blocking hardware monitoring service.
/// Continuously queries active hardware providers at a configurable cadence.
/// </summary>
public sealed class HardwareMonitorService : IHardwareMonitorService
{
    private readonly ILogger<HardwareMonitorService>? _logger;
    private readonly HardwareMonitorOptions _options;
    private readonly IHardwareProvider _primaryProvider;
    private readonly IHardwareProvider _fallbackProvider;
    private readonly ITelemetryWriter? _telemetryWriter;
    private readonly ITrendAnalyzer? _trendAnalyzer;

    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private bool _isDisposed;

    // Min/Max accumulator caches across ticks
    private readonly Dictionary<string, (double Min, double Max)> _minMaxTracker = new();
    private IReadOnlyList<TelemetryObservation> _latestObservations = Array.Empty<TelemetryObservation>();

    public event EventHandler<HardwareSnapshot>? TelemetryUpdated;
    public event EventHandler<IReadOnlyList<TelemetryObservation>>? ObservationsDetected;

    public TimeSpan PollingInterval { get; set; }
    public bool IsMonitoring { get; private set; }
    public HardwareSnapshot? CurrentSnapshot { get; private set; }
    public string? ActiveSessionId { get; set; }
    public IReadOnlyList<TelemetryObservation> LatestObservations => _latestObservations;

    public HardwareMonitorService(
        HardwareMonitorOptions? options = null,
        ITelemetryWriter? telemetryWriter = null,
        ITrendAnalyzer? trendAnalyzer = null,
        ILogger<HardwareMonitorService>? logger = null)
    {
        _logger = logger;
        _options = options ?? new HardwareMonitorOptions();
        PollingInterval = _options.DefaultPollingInterval;
        _telemetryWriter = telemetryWriter;
        _trendAnalyzer = trendAnalyzer;

        _primaryProvider = new LibreHardwareMonitorProvider();
        _fallbackProvider = new DiagnosticHardwareProvider();
    }

    // For unit testing with injected providers (Milestone 1 overload)
    public HardwareMonitorService(
        IHardwareProvider primaryProvider,
        IHardwareProvider fallbackProvider,
        HardwareMonitorOptions? options = null,
        ILogger<HardwareMonitorService>? logger = null)
    {
        _primaryProvider = primaryProvider;
        _fallbackProvider = fallbackProvider;
        _options = options ?? new HardwareMonitorOptions();
        PollingInterval = _options.DefaultPollingInterval;
        _logger = logger;
    }

    // Overload with telemetry writer and trend analyzer
    public HardwareMonitorService(
        IHardwareProvider primaryProvider,
        IHardwareProvider fallbackProvider,
        ITelemetryWriter telemetryWriter,
        ITrendAnalyzer? trendAnalyzer = null,
        HardwareMonitorOptions? options = null,
        ILogger<HardwareMonitorService>? logger = null)
    {
        _primaryProvider = primaryProvider;
        _fallbackProvider = fallbackProvider;
        _telemetryWriter = telemetryWriter;
        _trendAnalyzer = trendAnalyzer;
        _options = options ?? new HardwareMonitorOptions();
        PollingInterval = _options.DefaultPollingInterval;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsMonitoring) return Task.CompletedTask;

        _logger?.LogInformation("Starting hardware monitoring service (Interval: {Interval}ms)...", PollingInterval.TotalMilliseconds);

        // Attempt initialization of primary provider
        try
        {
            _primaryProvider.Initialize();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Primary hardware provider initialization encountered error: {Message}", ex.Message);
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsMonitoring = true;

        _monitoringTask = Task.Run(() => PollingLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task PollingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snapshot = await ExecutePollAsync(ct).ConfigureAwait(false);
                CurrentSnapshot = snapshot;
                TelemetryUpdated?.Invoke(this, snapshot);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception during hardware polling loop: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(PollingInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }

        IsMonitoring = false;
        _logger?.LogInformation("Hardware monitoring loop terminated cleanly.");
    }

    public async Task<HardwareSnapshot> RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await ExecutePollAsync(cancellationToken).ConfigureAwait(false);
        CurrentSnapshot = snapshot;
        TelemetryUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    private async Task<HardwareSnapshot> ExecutePollAsync(CancellationToken ct)
    {
        await _pollLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IReadOnlyList<HardwareItem> items = Array.Empty<HardwareItem>();

            // 1. Try primary LibreHardwareMonitor provider
            if (_primaryProvider.IsAvailable)
            {
                try
                {
                    items = _primaryProvider.PollHardware();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Primary provider failed polling: {Message}", ex.Message);
                }
            }

            // 2. If primary returned no hardware, fall back safely
            if (items.Count == 0)
            {
                items = _fallbackProvider.PollHardware();
            }

            // 3. Accumulate and track running Min and Max values
            foreach (var hw in items)
            {
                UpdateSensorMinMax(hw);
            }

            var snapshot = new HardwareSnapshot
            {
                SnapshotId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                IsElevated = !_primaryProvider.RequiresAdministrator,
                Hardware = items
            };

            // 4. Deterministic local trend analysis
            if (_trendAnalyzer != null)
            {
                var obs = _trendAnalyzer.AnalyzeSnapshot(snapshot);
                if (obs.Count > 0)
                {
                    _latestObservations = _trendAnalyzer.RecentObservations;
                    ObservationsDetected?.Invoke(this, obs);
                }
            }

            // 5. Non-blocking asynchronous telemetry persistence
            if (_telemetryWriter != null && !string.IsNullOrEmpty(ActiveSessionId))
            {
                _telemetryWriter.EnqueueSnapshot(snapshot, ActiveSessionId);
            }

            return snapshot;
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private void UpdateSensorMinMax(HardwareItem item)
    {
        foreach (var sensor in item.Sensors)
        {
            if (string.IsNullOrEmpty(sensor.HardwareId)) sensor.HardwareId = item.Id;
            if (string.IsNullOrEmpty(sensor.HardwareName)) sensor.HardwareName = item.Name;
            if (sensor.HardwareType == HardwareType.Unknown) sensor.HardwareType = item.Type;

            if (!sensor.Value.HasValue) continue;

            // Telemetry Quality sanity check
            var validation = PcSentinel.Core.Services.TelemetryValidator.Validate(sensor.SensorType, sensor.Value);
            if (!validation.IsUsable || !validation.ValidatedValue.HasValue)
            {
                continue;
            }

            double val = validation.ValidatedValue.Value;
            sensor.Value = val;

            if (_minMaxTracker.TryGetValue(sensor.SensorId, out var tracked))
            {
                double newMin = Math.Min(tracked.Min, val);
                double newMax = Math.Max(tracked.Max, val);
                _minMaxTracker[sensor.SensorId] = (newMin, newMax);
                sensor.Min = newMin;
                sensor.Max = newMax;
            }
            else
            {
                double initMin = sensor.Min ?? val;
                double initMax = sensor.Max ?? val;
                _minMaxTracker[sensor.SensorId] = (Math.Min(initMin, val), Math.Max(initMax, val));
                sensor.Min = _minMaxTracker[sensor.SensorId].Min;
                sensor.Max = _minMaxTracker[sensor.SensorId].Max;
            }
        }

        foreach (var sub in item.SubHardware)
        {
            UpdateSensorMinMax(sub);
        }
    }

    public async Task StopAsync()
    {
        if (!IsMonitoring) return;

        _logger?.LogInformation("Stopping hardware monitor service...");
        _cts?.Cancel();

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Exception while awaiting monitor task cancellation: {Message}", ex.Message);
            }
        }

        IsMonitoring = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _pollLock.Dispose();
        _primaryProvider.Dispose();
        _fallbackProvider.Dispose();
    }
}

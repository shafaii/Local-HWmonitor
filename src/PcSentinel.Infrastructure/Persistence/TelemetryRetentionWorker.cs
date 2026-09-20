using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.Infrastructure.Persistence;

/// <summary>
/// Background worker that enforces data retention policies by purging expired sensor readings.
/// Runs non-intrusively in the background without affecting UI responsiveness.
/// </summary>
public sealed class TelemetryRetentionWorker : IAsyncDisposable
{
    private readonly ITelemetryRepository _repository;
    private readonly ILogger<TelemetryRetentionWorker>? _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _workerTask;
    private RetentionPeriod _currentPeriod = RetentionPeriod.ThirtyDays;
    private bool _isDisposed;

    public RetentionPeriod CurrentRetentionPeriod
    {
        get => _currentPeriod;
        set => _currentPeriod = value;
    }

    public TelemetryRetentionWorker(
        ITelemetryRepository repository,
        RetentionPeriod initialPeriod = RetentionPeriod.ThirtyDays,
        ILogger<TelemetryRetentionWorker>? logger = null)
    {
        _repository = repository;
        _currentPeriod = initialPeriod;
        _logger = logger;
    }

    public void Start()
    {
        if (_workerTask != null) return;
        _workerTask = Task.Run(() => RunPeriodicRetentionLoopAsync(_cts.Token));
    }

    public async Task<int> ExecutePurgeAsync(CancellationToken cancellationToken = default)
    {
        var timespan = RetentionHelper.ToTimeSpan(_currentPeriod);
        if (!timespan.HasValue)
        {
            _logger?.LogInformation("Data retention is set to Unlimited. No readings purged.");
            return 0;
        }

        _logger?.LogInformation("Executing retention purge for period {Period} ({Duration})...", _currentPeriod, timespan.Value);
        return await _repository.PurgeExpiredReadingsAsync(timespan.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunPeriodicRetentionLoopAsync(CancellationToken ct)
    {
        // Initial delay to ensure application startup completes smoothly before retention I/O
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ExecutePurgeAsync(ct).ConfigureAwait(false);
                // Run purge every 6 hours
                await Task.Delay(TimeSpan.FromHours(6), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during background retention purge loop: {Message}", ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();
        if (_workerTask != null)
        {
            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
        _cts.Dispose();
    }
}

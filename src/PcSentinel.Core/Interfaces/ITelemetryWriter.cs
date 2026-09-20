using System;
using System.Threading;
using System.Threading.Tasks;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Interfaces;

/// <summary>
/// Asynchronous, non-blocking telemetry ingestion queue.
/// Decouples hardware sampling from disk I/O and protects the UI thread.
/// </summary>
public interface ITelemetryWriter : IAsyncDisposable
{
    /// <summary>
    /// Enqueues a full hardware snapshot for batched background persistence.
    /// Returns immediately without blocking the caller.
    /// </summary>
    bool EnqueueSnapshot(HardwareSnapshot snapshot, string sessionId);

    /// <summary>
    /// Flushes any pending sensor readings in the queue.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Current number of items waiting in the persistence queue.
    /// </summary>
    int PendingQueueCount { get; }

    /// <summary>
    /// Total readings successfully committed to disk during this application run.
    /// </summary>
    long TotalReadingsWritten { get; }
}

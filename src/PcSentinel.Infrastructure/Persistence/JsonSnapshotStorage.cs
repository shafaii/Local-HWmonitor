using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcSentinel.Core.Models;

namespace PcSentinel.Infrastructure.Persistence;

/// <summary>
/// Lightweight local file-based telemetry snapshot storage.
/// Serves as the persistence layer for Phase 1/2 before SQLite migration.
/// </summary>
public sealed class JsonSnapshotStorage : ISnapshotStorage
{
    private readonly string _storageDirectory;
    private readonly ILogger<JsonSnapshotStorage>? _logger;
    private readonly List<HardwareSnapshot> _inMemoryRingBuffer = new();
    private readonly object _lock = new();

    public JsonSnapshotStorage(string? storageDir = null, ILogger<JsonSnapshotStorage>? logger = null)
    {
        _logger = logger;
        _storageDirectory = storageDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCSentinel",
            "Snapshots");

        try
        {
            Directory.CreateDirectory(_storageDirectory);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not create storage directory: {Path}", _storageDirectory);
        }
    }

    public Task SaveSnapshotAsync(HardwareSnapshot snapshot, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _inMemoryRingBuffer.Add(snapshot);
            if (_inMemoryRingBuffer.Count > 1000)
            {
                _inMemoryRingBuffer.RemoveAt(0);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HardwareSnapshot>> LoadRecentSnapshotsAsync(int limit = 50, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _inMemoryRingBuffer
                .OrderByDescending(s => s.Timestamp)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<HardwareSnapshot>>(results);
        }
    }

    public Task ClearHistoryAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _inMemoryRingBuffer.Clear();
        }

        return Task.CompletedTask;
    }
}

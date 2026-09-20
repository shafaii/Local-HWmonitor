using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PcSentinel.Core.Models;

namespace PcSentinel.Infrastructure.Persistence;

public interface ISnapshotStorage
{
    Task SaveSnapshotAsync(HardwareSnapshot snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<HardwareSnapshot>> LoadRecentSnapshotsAsync(int limit = 50, CancellationToken ct = default);
    Task ClearHistoryAsync(CancellationToken ct = default);
}

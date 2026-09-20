using System;
using System.Collections.Generic;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Interfaces;

/// <summary>
/// Low-level abstraction over hardware sensor discovery and interrogation engines.
/// </summary>
public interface IHardwareProvider : IDisposable
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    bool RequiresAdministrator { get; }

    /// <summary>
    /// Initializes discovery of motherboard, CPU, GPU, Memory, and storage.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Queries the latest sensor values across all hardware components.
    /// </summary>
    IReadOnlyList<HardwareItem> PollHardware();
}

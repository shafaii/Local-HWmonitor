# PC Sentinel — Architecture & System Design Specification

## 1. Architectural Philosophy

PC Sentinel adheres to Clean Architecture and strict separation of concerns. Hardware monitoring utilities frequently suffer from tight coupling between low-level ring0 driver wrappers and presentation UI code, leading to fragility, memory leaks, unhandled driver crashes, and unmaintainable codebases.

PC Sentinel solves this by enforcing strict domain boundaries:
- The UI layer (`PcSentinel.App`) has zero knowledge of `LibreHardwareMonitorLib`.
- The domain layer (`PcSentinel.Core`) contains pure business logic and normalized models.
- The hardware layer (`PcSentinel.Hardware`) adapts low-level telemetry sources into normalized streams.
- The AI layer (`PcSentinel.AI`) operates exclusively on sanitized `DiagnosticSnapshot` models.

```
┌─────────────────────────────────────────────────────────────┐
│                 Presentation: PcSentinel.App                │
│    WinUI 3 / XAML Views • ViewModels (CommunityToolkit)    │
└──────────────────────────────┬──────────────────────────────┘
                               │ Observes (INotifyPropertyChanged)
┌──────────────────────────────▼──────────────────────────────┐
│                    Domain: PcSentinel.Core                  │
│   HardwareItem • Sensor • SystemHealthState • Snapshots     │
│             IHardwareMonitorService contract                │
└───────────────▲──────────────────────────────▲──────────────┘
                │ Implements                   │ Analyzes
┌───────────────┴──────────────┐ ┌─────────────┴──────────────┐
│ Hardware: PcSentinel.Hardware│ │      AI: PcSentinel.AI     │
│   LibreHardwareMonitorLib    │ │  Gemini Diagnostic Engine  │
│   Sensor Visitor & Poller    │ │  Telemetry Summarizer      │
└──────────────────────────────┘ └────────────────────────────┘
```

---

## 2. Project Boundaries & Responsibilities

### `PcSentinel.Core`
- **Zero External Dependencies**: Depends only on the .NET BCL.
- **Domain Models**:
  - `HardwareItem`: Represents physical or logical components (CPU, GPU, Memory, Drive, Motherboard).
  - `Sensor`: Represents a discrete telemetry channel (Temperature, Load, Clock, Fan, Voltage, Power).
  - `SensorSnapshot`: An immutable capture of all active sensors at a specific timestamp.
  - `SystemHealthState`: Evaluates overall thermal headroom, throttling status, and load anomalies.
- **Interfaces**:
  - `IHardwareMonitorService`: Contract for lifecycle management (Start, Stop, Pause, SetInterval).
  - `IHardwareProvider`: Abstraction over underlying telemetry engines.
  - `ISnapshotStorage`: Local persistence contract.

### `PcSentinel.Hardware`
- **Hardware Telemetry Engine**: Bridges `LibreHardwareMonitorLib` to the core domain.
- **Visitor Pattern**: Implements `IVisitor` (`VisitComputer`, `VisitHardware`, `VisitSensor`) to traverse component hierarchies without recursive allocation.
- **Normalizer**: Translates raw `LibreHardwareMonitor.Hardware.SensorType` and units into normalized, high-precision domain models.
- **Privilege & Exception Shielding**:
  - Detects Windows Administrator elevation status.
  - Catches driver initialization exceptions (`WinRing0`, `InpOutx64`) without crashing the process.
  - Exposes an elevation warning banner if kernel-level sensors (e.g., VRM temperatures, memory timings) are blocked by standard user permissions.

### `PcSentinel.App`
- **Native Windows Presentation**: Built with WinUI 3 and Windows App SDK.
- **MVVM Pattern**: ViewModels inherit from `ObservableObject` and utilize source generators (`[ObservableProperty]`, `[RelayCommand]`) from `CommunityToolkit.Mvvm`.
- **Thread Safety**: Telemetry updates arriving from background worker threads are dispatched safely to the UI thread via `DispatcherQueue.TryEnqueue`.
- **Pages**:
  - `DashboardPage`: High-level summary cards (CPU, GPU, RAM, Storage) and real-time sensor feed.
  - `HardwarePage`: Deep tree navigation mirroring physical motherboard topology.
  - `DiagnosticsPage`: Real-time health assessment and snapshot generation.
  - `HistoryPage`, `AlertsPage`, `SettingsPage`.

---

## 3. Sensor Polling Lifecycle

```
[UI / App Startup]
       │
       ▼
IHardwareMonitorService.StartAsync(interval: 1000ms)
       │
       ▼
[Background Task on ThreadPool]
  ┌──► Wait for Timer / PeriodicTimer (1000ms)
  │    Check CancellationToken
  │    │
  │    ▼
  │    IHardwareProvider.PollSensors()
  │    │──> LibreHardwareMonitor.Computer.Accept(visitor)
  │    │──> Normalizer converts raw sensors -> domain models
  │    │
  │    ▼
  │    Publish HardwareUpdated event (immutable or updated snapshot)
  │    │
  │    ▼
  │    ViewModel receives update -> DispatcherQueue.TryEnqueue -> UI updates
  └─── Loop
```

---

## 4. Error Handling & Graceful Degradation

| Scenario | Behavior |
| :--- | :--- |
| **Missing GPU (e.g., Headless/VM)** | GPU card gracefully displays "No Discrete GPU Detected"; CPU, Memory, and Storage continue monitoring normally. |
| **No Administrator Privileges** | Application operates in standard mode; warning indicator informs user that ring0 sensors require elevation. |
| **Sensor Disconnection (e.g., Removable Drive)** | Sensor status transitions to inactive or retained with last known state flagged as stale. |
| **High Polling Load** | If a polling tick takes longer than the interval, consecutive ticks are skipped rather than queuing up behind locks. |

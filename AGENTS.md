# AGENTS.md — PC Sentinel Engineering Guidelines & Architectural Directives

## 1. Project Mission

**PC Sentinel** is a professional Windows hardware monitoring and AI-assisted diagnostic desktop workstation application. It is designed for engineers, power users, and system administrators who require real-time hardware telemetry, deep sensor inspection, local historical telemetry, and intelligent diagnostic insights.

PC Sentinel draws inspiration from trusted tools like HWiNFO, HWMonitor, Libre Hardware Monitor, and Windows Task Manager, but avoids cloning their interfaces. Instead, it provides a modern, restrained, native Windows 11 Fluent Design experience.

Crucially:
- **Telemetry First**: Hardware telemetry must be reliable, continuous, and non-blocking.
- **AI As an Assistant, Not a Crutch**: Telemetry must remain fully functional and useful even if AI/Gemini integration is disabled or offline.
- **Zero Hallucination / Zero Fake Telemetry**: Never fabricate hardware metrics or simulate nonexistent hardware without explicit diagnostic flags.

---

## 2. Technology Stack & Platform Mandates

- **Target Platform**: Windows 10/11 x64 (desktop application)
- **Language & Runtime**: C# with .NET (targeting .NET 9 / .NET 10)
- **UI Framework**: WinUI 3 with Windows App SDK and XAML
- **Architecture Pattern**: Model-View-ViewModel (MVVM) using `CommunityToolkit.Mvvm`
- **Dependency Injection**: `Microsoft.Extensions.DependencyInjection`
- **Logging**: `Microsoft.Extensions.Logging` (structured, high-performance)
- **Hardware Access**: `LibreHardwareMonitorLib` (via NuGet) for ring0/WMI/SMBIOS sensor discovery
- **Persistence (Phases 2+)**: Local SQLite database for historical sensor aggregation
- **AI Integration (Phase 5+)**: Official Google Gen AI .NET SDK for contextual diagnostic summaries
- **Forbidden Technologies for Core Desktop**: Do NOT use Electron, Python runtime, browser wrappers, or cloud-hosted telemetry pipelines.

---

## 3. Solution & Repository Architecture

The codebase enforces strict separation of concerns across decoupled assemblies:

```
src/
├── PcSentinel.Core/           # Pure domain models, sensor contracts, health states, interfaces
├── PcSentinel.Hardware/       # LibreHardwareMonitorLib adapter, sensor polling, visitor pattern, normalization
├── PcSentinel.Infrastructure/ # Local storage (SQLite/JSON), Windows service utilities, system logs
├── PcSentinel.AI/             # Diagnostic context builders, snapshot packagers, Gemini client (Phase 5)
└── PcSentinel.App/            # WinUI 3 XAML views, ViewModels, navigation, presentation logic

tests/
├── PcSentinel.Core.Tests/     # Unit tests for domain models, normalization, snapshots, and math
└── PcSentinel.Hardware.Tests/ # Sensor normalizer tests, polling lifecycle, visitor tests

docs/
├── ARCHITECTURE.md            # Boundary documentation and data-flow diagrams
└── ROADMAP.md                 # 6-phase engineering roadmap
```

### Decoupling Mandate
- `PcSentinel.Core` MUST NOT reference `LibreHardwareMonitorLib` or any Windows-specific UI assemblies.
- LibreHardwareMonitor objects (`IHardware`, `ISensor`) MUST NEVER leak directly into ViewModels or UI XAML.
- All hardware elements are mapped into normalized domain models (`HardwareItem`, `Sensor`, `SensorSnapshot`).

---

## 4. Coding & Engineering Rules

1. **Null Safety**: Nullable reference types (`<Nullable>enable</Nullable>`) are enforced across all projects. No unannotated nullables.
2. **Asynchronous Discipline**:
   - Hardware sensor polling MUST NEVER run on or block the UI thread.
   - Long-running polling loops must accept `CancellationToken` and exit cleanly.
   - Use `Task.Delay(interval, token)` rather than thread sleeps.
3. **Allocation Awareness**:
   - Polling runs at 1 Hz (or faster). Avoid allocating large object graphs on every tick.
   - Sensor update passes should mutate existing model instances or use lightweight structs/records where appropriate.
4. **Exception Handling**:
   - Never silently swallow exceptions.
   - Hardware sensor access will encounter `UnauthorizedAccessException` if ring0 drivers or WMI require elevation. Gracefully flag sensors as requiring Administrator privileges and surface this in the UI.
   - Missing or unsupported sensors on certain hardware types must degrade gracefully without crashing.
5. **Dependency Injection**:
   - ViewModels, services, and hardware providers must be registered through `IServiceCollection`.
   - Avoid static global states; use constructor injection.

---

## 5. UI & Design Principles (Windows Fluent Design)

- **Aesthetic**: Follow modern Windows 11 Fluent Design guidelines.
- **Backdrop**: Support Mica and Acrylic materials where appropriate.
- **Theme**: Dark mode by default with balanced contrast (WCAG AA compliant).
- **Navigation**: Clean left `NavigationView` with standard sections:
  1. *Dashboard* (Milestone 1 — Summary cards + live stream)
  2. *Hardware* (Milestone 1 — Comprehensive sensor tree)
  3. *History* (Placeholder in M1, populated in Phase 2)
  4. *Diagnostics* (Telemetry snapshot & AI assistant)
  5. *Alerts* (Threshold alarms & warnings)
  6. *Settings* (Polling interval, unit toggles, admin elevation check)
- **Responsiveness**: Smooth UI rendering that never stutters during background sensor queries.

---

## 6. Telemetry & Sensor Normalization Rules

A normalized `Sensor` model represents:
- `HardwareId`: string (unique identifier)
- `HardwareName`: string (e.g., "Intel Core i9-13900K", "NVIDIA GeForce RTX 4090")
- `HardwareType`: enum (`Cpu`, `GpuNvidia`, `GpuAmd`, `GpuIntel`, `Memory`, `Storage`, `Motherboard`, `Network`, `Cooler`)
- `SensorId`: string
- `SensorName`: string (e.g., "Core Max", "GPU Hot Spot", "Memory Used")
- `SensorType`: enum (`Temperature`, `Load`, `Clock`, `Fan`, `Voltage`, `Current`, `Power`, `Data`, `Throughput`, `Level`, `Factor`)
- `Value`: double? (nullable to support disconnected or pending sensors)
- `Min`: double?
- `Max`: double?
- `Unit`: string (e.g., "°C", "%", "MHz", "RPM", "V", "W", "GB", "MB/s")
- `Timestamp`: DateTimeOffset

---

## 7. AI Integration & Privacy Rules (Gemini)

1. **User-Initiated Only**: Telemetry data is NEVER automatically transmitted to any cloud endpoint. Snapshots are only generated and sent when the user explicitly requests diagnosis.
2. **Sanitization**: Before packaging a telemetry snapshot for Gemini:
   - Strip serial numbers, MAC addresses, drive volume labels with personal names, and file paths.
   - Retain only hardware models, temperature profiles, throttling flags, voltage deviations, and load histories.
3. **Compact Format**: Diagnostic snapshots must be structured as compact JSON or Markdown tables to minimize token usage and maximize diagnostic reasoning accuracy.

---

## 8. Testing Expectations

- **Core Tests**: Unit tests must cover sensor normalization, unit formatting, min/max tracking, health state evaluations, and snapshot serialization.
- **Hardware Tests**: Test sensor visitors and mock hardware trees to guarantee polling stability and error handling without requiring physical ring0 drivers during automated CI.
- All unit tests must pass cleanly via `dotnet test`.

---

## 9. Definition of Done for Milestone 1

1. Solution builds with zero errors.
2. WinUI 3 application architecture and MVVM components compiled and runnable.
3. LibreHardwareMonitorLib integrated with clean visitor and polling abstraction.
4. Real hardware discovery of CPU, GPU, Memory, and Storage devices.
5. Available sensor values normalized and updating at 1-second intervals without freezing the UI.
6. Graceful handling of missing sensors and non-admin elevation restrictions.
7. Structured logging and unit tests passing.
8. Comprehensive documentation: `README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`.

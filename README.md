# PC Sentinel — Professional Windows Hardware Monitoring & AI Diagnostics

[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-blue.svg)](https://microsoft.com)
[![Framework: .NET 9 / 10](https://img.shields.io/badge/Framework-.NET%209%20%2F%2010-purple.svg)](https://dotnet.microsoft.com)
[![UI: WinUI 3](https://img.shields.io/badge/UI-WinUI%203%20%2F%20Windows%20App%20SDK-0078D4.svg)](https://learn.microsoft.com/windows/apps/winui/)
[![Architecture: MVVM](https://img.shields.io/badge/Architecture-Clean%20%2F%20MVVM-green.svg)](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)

**PC Sentinel** is a professional, native Windows desktop application for continuous hardware telemetry, sensor normalization, and AI-assisted diagnostics. Inspired by industry-standard utilities like HWiNFO, HWMonitor, and Windows Task Manager, PC Sentinel combines low-overhead kernel sensor polling with an authentic Windows 11 Fluent Design presentation.

---

## Key Features (Milestone 1)

- **Native Windows 11 Fluent UI**: Crafted in WinUI 3 with Mica backdrops, dark theme support, restrained typography, and fluid status indicators.
- **Hardware Telemetry via LibreHardwareMonitorLib**: Discovers and monitors CPU, GPU (NVIDIA/AMD/Intel), Memory, Storage drives, and Motherboards.
- **Normalized Sensor Domain**: Unifies temperatures, loads, clock speeds, fan RPMs, voltages, currents, and power metrics into strongly typed domain models.
- **Non-Blocking Polling Engine**: Runs background sensor queries on configurable intervals (default 1000 ms) using asynchronous worker threads.
- **Graceful Error Handling**: Detects missing sensors or restricted ring0 permissions when running without Administrator privileges.
- **AI-Ready Diagnostic Architecture**: Built-in snapshot serialization prepared for contextual Gemini analysis in future phases.

---

## Solution Structure

```
src/
├── PcSentinel.App/           # WinUI 3 desktop application, Views, ViewModels, DI container
├── PcSentinel.Core/          # Domain models, sensor types, health evaluations, interfaces
├── PcSentinel.Hardware/      # LibreHardwareMonitorLib adapter, visitors, sensor normalization
├── PcSentinel.Infrastructure/# Logging utilities, configuration, persistence contracts
└── PcSentinel.AI/            # Telemetry snapshot builder, Gemini client contracts

tests/
├── PcSentinel.Core.Tests/    # Unit tests for domain models, health states, and normalization
└── PcSentinel.Hardware.Tests/# Sensor provider lifecycle and visitor tests

docs/
├── ARCHITECTURE.md           # System boundaries and data flow
└── ROADMAP.md                # 6-phase engineering progression
```

---

## Requirements & Building

### Prerequisites
- **Operating System**: Windows 10 (version 1809 or higher, build 17763+) or Windows 11
- **Architecture**: x64
- **SDK**: .NET 9.0 SDK or .NET 10.0 SDK
- **Workloads**: `.NET Desktop Development` and `Windows App SDK` workload in Visual Studio 2022 / CLI

### Building from Command Line
```powershell
# Restore all dependencies
dotnet restore PcSentinel.sln

# Build the complete solution
dotnet build PcSentinel.sln -c Release

# Run automated tests
dotnet test tests/PcSentinel.Core.Tests/PcSentinel.Core.Tests.csproj
dotnet test tests/PcSentinel.Hardware.Tests/PcSentinel.Hardware.Tests.csproj
```

### Running the Application
```powershell
dotnet run --project src/PcSentinel.App/PcSentinel.App.csproj
```

---

## Administrator Privilege Considerations

Low-level motherboard, CPU core temperature registers, and GPU VRM sensors communicate with hardware via ring0 kernel drivers (`WinRing0` / `InpOutx64`).
- **Standard User**: PC Sentinel discovers logical hardware, memory utilization, storage SMART status, and standard WMI counters.
- **Administrator**: Run PC Sentinel as Administrator to unlock embedded controller (EC) fan telemetry, CPU package wattages, and low-level voltage rails. An elevation status banner appears in the application window to clearly explain current privilege level.

---

## Open Source Dependencies & Acknowledgments

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL 2.0)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT)
- [Windows App SDK / WinUI 3](https://github.com/microsoft/WindowsAppSDK) (MIT)
- [Microsoft.Extensions.DependencyInjection & Logging](https://github.com/dotnet/runtime) (MIT)

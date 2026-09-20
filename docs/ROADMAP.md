# PC Sentinel — Product Roadmap

This document outlines the six planned engineering phases for PC Sentinel.

---

## Phase 1 — Reliable Telemetry (Milestone 1 — Completed)
- [x] Repository and multi-project solution structure established.
- [x] Domain models and sensor normalization engine (`PcSentinel.Core`).
- [x] LibreHardwareMonitorLib integration with visitor pattern (`PcSentinel.Hardware`).
- [x] Background polling engine with non-blocking 1-second ticks.
- [x] Windows App SDK / WinUI 3 presentation layer with CommunityToolkit.Mvvm (`PcSentinel.App`).
- [x] Dashboard summary cards for CPU, GPU, Memory, and Storage.
- [x] Real-time live sensor list with type filtering.
- [x] Hardware topology tree view.
- [x] Graceful degradation for missing sensors and non-administrator execution.
- [x] Unit test suite for domain and normalization logic.
- [x] Comprehensive architectural and developer documentation.

---

## Phase 2 — Historical Monitoring & Local Persistence (Milestone 2 — Completed)
- [x] Local SQLite telemetry storage in WAL mode (`SqliteTelemetryRepository`).
- [x] Producer/consumer non-blocking channel writer (`ChannelTelemetryWriter`).
- [x] Monitoring session tracking with auto-recovery of interrupted sessions (`MonitoringSession`).
- [x] Multi-tier adaptive downsampling aggregation pipeline (`HistoricalQueryService`).
- [x] Interactive historical hardware telemetry graphs (`HistoryPage`, `HistoryViewModel`).
- [x] Configurable data retention with automated background worker (`TelemetryRetentionWorker`, `SettingsPage`).
- [x] Deterministic trend analysis engine detecting thermal spikes and sustained load (`TrendAnalyzer`, `AlertsPage`).
- [x] Real-time dashboard sparkline visualizers with 30-second rolling history (`HardwareCardViewModel`).
- [x] Physical sanity and numerical validation engine (`TelemetryValidator`).
- [x] Comprehensive test suite for persistence, queries, downsampling, and trend analysis.

---

## Phase 3 — Alerts & Anomaly Detection
- [ ] Configurable threshold engine (e.g., CPU Temp > 95°C, Memory Utilization > 92%).
- [ ] Thermal throttling detection (PROCHOT, thermal flag registers).
- [ ] Voltage drop / power ripple anomaly detection.
- [ ] Windows Toast Notifications for critical thermal or power thresholds.
- [ ] System Tray icon with live temperature/load badges and minimize-to-tray.

---

## Phase 4 — Windows Diagnostics & Event Logs
- [ ] Windows Event Log integration (System and Application logs).
- [ ] Detection of WHEA (Windows Hardware Error Architecture) events.
- [ ] Driver crash detection (Display driver timeout detection & recovery / TDR).
- [ ] Storage health S.M.A.R.T. status and reallocated sector warnings.
- [ ] Power state transition logging (Sleep/Wake/Hibernate/Kernel-Power 41).

---

## Phase 5 — Gemini AI Diagnosis
- [ ] Structured diagnostic snapshot packaging with user-sanitized hardware telemetry.
- [ ] Integration with the official Google Gen AI .NET SDK.
- [ ] Diagnostic prompts tailored for PC enthusiasts, overclockers, and support technicians.
- [ ] Potential cause explanations for thermal throttling, hardware instability, and frame drops.
- [ ] Interactive Q&A chat interface ("Why is my CPU running at 90°C while idle?").
- [ ] Complete offline fallback mode: AI features cleanly disabled with zero degradation to real-time monitoring.

---

## Phase 6 — Packaging & Production Polish
- [ ] MSIX packaging and Windows Store readiness.
- [ ] Authenticode code signing with EV certificate.
- [ ] Windows 11 context menu integration and auto-start on boot toggle.
- [ ] Multi-monitor and high-DPI scaling validation.
- [ ] Localization and multi-language support.

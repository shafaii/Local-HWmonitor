using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcSentinel.AI.Diagnostics;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IHardwareMonitorService _monitorService;
    private SensorType? _activeFilterType;

    public DashboardViewModel(IHardwareMonitorService monitorService)
    {
        _monitorService = monitorService;
        _monitorService.TelemetryUpdated += OnTelemetryUpdated;

        CpuCard = new HardwareCardViewModel { Title = "Processor (CPU)", HardwareType = HardwareType.Cpu };
        GpuCard = new HardwareCardViewModel { Title = "Graphics (GPU)", HardwareType = HardwareType.GpuNvidia };
        MemoryCard = new HardwareCardViewModel { Title = "Memory (RAM)", HardwareType = HardwareType.Memory };
        StorageCard = new HardwareCardViewModel { Title = "Primary Storage", HardwareType = HardwareType.Storage };
    }

    [ObservableProperty]
    private HardwareCardViewModel _cpuCard;

    [ObservableProperty]
    private HardwareCardViewModel _gpuCard;

    [ObservableProperty]
    private HardwareCardViewModel _memoryCard;

    [ObservableProperty]
    private HardwareCardViewModel _storageCard;

    [ObservableProperty]
    private string _systemHealthStatus = "Healthy — Nominal Parameters";

    [ObservableProperty]
    private string _lastUpdatedText = "Initializing telemetry stream...";

    [ObservableProperty]
    private bool _isMonitoring = true;

    [ObservableProperty]
    private int _totalSensorCount;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private string _diagnosticDigest = string.Empty;

    public ObservableCollection<SensorViewModel> FilteredSensors { get; } = new();
    private readonly System.Collections.Generic.List<Sensor> _allRawSensors = new();

    private void OnTelemetryUpdated(object? sender, HardwareSnapshot snapshot)
    {
        App.RunOnUIThread(() =>
        {
            // Update summary cards
            UpdateCards(snapshot);

            // Update health state
            var health = SystemHealthState.Evaluate(snapshot);
            SystemHealthStatus = health.OverallSeverity switch
            {
                HealthSeverity.Critical => "CRITICAL — Thermal or electrical limit reached",
                HealthSeverity.Warning => "WARNING — Elevated temperatures detected",
                HealthSeverity.Elevated => "ELEVATED — Moderate hardware stress",
                _ => "Optimal — All sensors within nominal limits"
            };

            LastUpdatedText = $"Live • {snapshot.Timestamp:HH:mm:ss} UTC (1000ms polling)";

            // Cache sensors and refresh filtered collection
            _allRawSensors.Clear();
            _allRawSensors.AddRange(snapshot.AllSensors);
            TotalSensorCount = _allRawSensors.Count;

            ApplyFilter();

            DiagnosticDigest = DiagnosticSnapshotBuilder.BuildTelemetryDigest(snapshot);
        });
    }

    private void UpdateCards(HardwareSnapshot snapshot)
    {
        // 1. CPU
        if (snapshot.Cpu != null)
        {
            CpuCard.IsAvailable = true;
            CpuCard.Subtitle = snapshot.Cpu.Name;
            var load = snapshot.Cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Total", StringComparison.OrdinalIgnoreCase))?.Value ?? 0;
            var temp = snapshot.Cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.FormattedValue ?? "—";
            var clock = snapshot.Cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock)?.FormattedValue ?? "—";
            var power = snapshot.Cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power)?.FormattedValue ?? "—";

            CpuCard.PrimaryPercentage = load;
            CpuCard.PrimaryPercentageFormatted = $"{load:0.#}%";
            CpuCard.TemperatureText = temp;
            CpuCard.ClockText = clock;
            CpuCard.PowerOrCapacityText = power;
            CpuCard.StatusTag = load > 90 ? "High Load" : (temp.Contains("9") ? "Thermal Throttling" : "Normal");
            CpuCard.StatusColor = load > 90 ? "#D83B01" : "#107C41";
            CpuCard.AddSparklineSample(load);
        }

        // 2. GPU
        if (snapshot.Gpu != null)
        {
            GpuCard.IsAvailable = true;
            GpuCard.Subtitle = snapshot.Gpu.Name;
            var load = snapshot.Gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load)?.Value ?? 0;
            var temp = snapshot.Gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.FormattedValue ?? "—";
            var fan = snapshot.Gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan)?.FormattedValue ?? "0 RPM";
            var clock = snapshot.Gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock)?.FormattedValue ?? "—";

            GpuCard.PrimaryPercentage = load;
            GpuCard.PrimaryPercentageFormatted = $"{load:0.#}%";
            GpuCard.TemperatureText = temp;
            GpuCard.ClockText = clock;
            GpuCard.PowerOrCapacityText = fan;
            GpuCard.StatusTag = "Active";
            GpuCard.StatusColor = "#107C41";
            GpuCard.AddSparklineSample(load);
        }
        else
        {
            GpuCard.IsAvailable = false;
            GpuCard.Subtitle = "No Discrete GPU Detected";
            GpuCard.StatusTag = "Offline";
            GpuCard.StatusColor = "#797775";
        }

        // 3. Memory
        if (snapshot.Memory != null)
        {
            MemoryCard.IsAvailable = true;
            MemoryCard.Subtitle = snapshot.Memory.Name;
            var load = snapshot.Memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load)?.Value ?? 0;
            var used = snapshot.Memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.SensorName.Contains("Used", StringComparison.OrdinalIgnoreCase))?.FormattedValue ?? "—";
            var available = snapshot.Memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.SensorName.Contains("Available", StringComparison.OrdinalIgnoreCase))?.FormattedValue ?? "—";

            MemoryCard.PrimaryPercentage = load;
            MemoryCard.PrimaryPercentageFormatted = $"{load:0.#}%";
            MemoryCard.TemperatureText = $"Avail: {available}";
            MemoryCard.ClockText = "DDR5 / Dual Channel";
            MemoryCard.PowerOrCapacityText = $"Used: {used}";
            MemoryCard.StatusTag = load > 90 ? "High Usage" : "Normal";
            MemoryCard.StatusColor = load > 90 ? "#D83B01" : "#107C41";
            MemoryCard.AddSparklineSample(load);
        }

        // 4. Storage
        var primaryDrive = snapshot.StorageDrives.FirstOrDefault();
        if (primaryDrive != null)
        {
            StorageCard.IsAvailable = true;
            StorageCard.Subtitle = primaryDrive.Name;
            var usedPercent = primaryDrive.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load)?.Value ?? 0;
            var freeGb = primaryDrive.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data)?.FormattedValue ?? "—";
            var temp = primaryDrive.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.FormattedValue ?? "—";

            StorageCard.PrimaryPercentage = usedPercent;
            StorageCard.PrimaryPercentageFormatted = $"{usedPercent:0.#}%";
            StorageCard.TemperatureText = temp;
            StorageCard.ClockText = "NVMe PCIe 4.0";
            StorageCard.PowerOrCapacityText = $"Free: {freeGb}";
            StorageCard.StatusTag = "Healthy";
            StorageCard.StatusColor = "#107C41";
            StorageCard.AddSparklineSample(usedPercent);
        }
    }

    [RelayCommand]
    public void SetFilter(string filterName)
    {
        SelectedFilter = filterName;
        _activeFilterType = filterName switch
        {
            "Temperatures" => SensorType.Temperature,
            "Loads" => SensorType.Load,
            "Clocks" => SensorType.Clock,
            "Fans" => SensorType.Fan,
            "Power" => SensorType.Power,
            "Memory" => SensorType.Data,
            _ => null
        };

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var matched = _allRawSensors
            .Where(s => !_activeFilterType.HasValue || s.SensorType == _activeFilterType.Value)
            .OrderBy(s => s.HardwareName)
            .ThenBy(s => s.SensorType)
            .ThenBy(s => s.SensorName)
            .Take(80);

        FilteredSensors.Clear();
        foreach (var s in matched)
        {
            FilteredSensors.Add(new SensorViewModel(s));
        }
    }

    [RelayCommand]
    public async Task RefreshNowAsync()
    {
        await _monitorService.RefreshNowAsync();
    }

    [RelayCommand]
    public async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
        {
            await _monitorService.StopAsync();
            IsMonitoring = false;
            LastUpdatedText = "Monitoring Paused";
        }
        else
        {
            await _monitorService.StartAsync();
            IsMonitoring = true;
            LastUpdatedText = "Resuming telemetry...";
        }
    }
}

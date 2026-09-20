using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.App.ViewModels;

public sealed partial class HardwareViewModel : ObservableObject
{
    private readonly IHardwareMonitorService _monitorService;

    public HardwareViewModel(IHardwareMonitorService monitorService)
    {
        _monitorService = monitorService;
        _monitorService.TelemetryUpdated += OnTelemetryUpdated;
    }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private HardwareItem? _selectedHardware;

    public ObservableCollection<HardwareItem> HardwareTree { get; } = new();
    public ObservableCollection<SensorViewModel> SelectedHardwareSensors { get; } = new();

    private void OnTelemetryUpdated(object? sender, HardwareSnapshot snapshot)
    {
        App.RunOnUIThread(() =>
        {
            HardwareTree.Clear();
            foreach (var hw in snapshot.Hardware)
            {
                HardwareTree.Add(hw);
            }

            if (SelectedHardware == null && snapshot.Hardware.Count > 0)
            {
                SelectedHardware = snapshot.Hardware[0];
            }

            UpdateSelectedSensors();
        });
    }

    partial void OnSearchQueryChanged(string value)
    {
        UpdateSelectedSensors();
    }

    partial void OnSelectedHardwareChanged(HardwareItem? value)
    {
        UpdateSelectedSensors();
    }

    private void UpdateSelectedSensors()
    {
        SelectedHardwareSensors.Clear();
        if (SelectedHardware == null) return;

        var sensors = SelectedHardware.GetAllSensors();
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            sensors = sensors.Where(s =>
                s.SensorName.Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase) ||
                s.SensorType.ToString().Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase));
        }

        foreach (var s in sensors)
        {
            SelectedHardwareSensors.Add(new SensorViewModel(s));
        }
    }
}

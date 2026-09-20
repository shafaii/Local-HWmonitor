using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcSentinel.Core.Interfaces;

namespace PcSentinel.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IHardwareMonitorService _monitorService;

    public MainViewModel(
        IHardwareMonitorService monitorService,
        DashboardViewModel dashboardVm,
        HardwareViewModel hardwareVm,
        HistoryViewModel historyVm,
        DiagnosticsViewModel diagnosticsVm,
        AlertsViewModel alertsVm,
        SettingsViewModel settingsVm)
    {
        _monitorService = monitorService;
        Dashboard = dashboardVm;
        Hardware = hardwareVm;
        History = historyVm;
        Diagnostics = diagnosticsVm;
        Alerts = alertsVm;
        Settings = settingsVm;

        CurrentViewModel = Dashboard;
    }

    public DashboardViewModel Dashboard { get; }
    public HardwareViewModel Hardware { get; }
    public HistoryViewModel History { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public AlertsViewModel Alerts { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _selectedNavTag = "Dashboard";

    [ObservableProperty]
    private bool _isElevated = false;

    [ObservableProperty]
    private string _elevationStatusText = "Standard Mode • Some ring0 sensors may require Administrator";

    [RelayCommand]
    public void Navigate(string tag)
    {
        SelectedNavTag = tag;
        CurrentViewModel = tag switch
        {
            "Dashboard" => Dashboard,
            "Hardware" => Hardware,
            "History" => History,
            "Diagnostics" => Diagnostics,
            "Alerts" => Alerts,
            "Settings" => Settings,
            _ => Dashboard
        };
    }

    public async Task InitializeAsync()
    {
        await _monitorService.StartAsync();
        if (_monitorService.CurrentSnapshot != null)
        {
            IsElevated = _monitorService.CurrentSnapshot.IsElevated;
            ElevationStatusText = IsElevated
                ? "Elevated (Administrator) • Full hardware driver access active"
                : "Standard User Mode • Running with safe WMI and user-mode diagnostics";
        }
    }
}

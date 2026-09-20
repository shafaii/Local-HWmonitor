using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using PcSentinel.App.ViewModels;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Services;
using PcSentinel.Hardware.Monitoring;
using PcSentinel.Infrastructure.Persistence;

namespace PcSentinel.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue { get; private set; }
    private Window? _mainWindow;

    public static void RunOnUIThread(Action action)
    {
        if (DispatcherQueue != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try { action(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"UI thread exception: {ex}"); }
            });
        }
        else
        {
            action();
        }
    }

    public App()
    {
        UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[PcSentinel.App] Global UnhandledException caught: {e.Message} - {e.Exception}");
            e.Handled = true;
        };

        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Local SQLite Telemetry Database
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "PCSentinel");
        string dbPath = Path.Combine(appFolder, "telemetry.db");

        var repository = new SqliteTelemetryRepository(dbPath);
        services.AddSingleton<ITelemetryRepository>(repository);
        services.AddSingleton(repository);

        // Telemetry Writer (Producer/Consumer Channel Queue)
        services.AddSingleton<ITelemetryWriter, ChannelTelemetryWriter>();

        // Historical Query & Downsampling Service
        services.AddSingleton<IHistoricalQueryService, HistoricalQueryService>();

        // Deterministic Trend Analyzer
        services.AddSingleton<ITrendAnalyzer, TrendAnalyzer>();

        // Background Retention Worker
        services.AddSingleton<TelemetryRetentionWorker>();

        // Core Hardware Monitor Service
        services.AddSingleton<IHardwareMonitorService, HardwareMonitorService>();
        services.AddSingleton<ISnapshotStorage, JsonSnapshotStorage>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<HardwareViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<AlertsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[PcSentinel.App] Global UnhandledException caught: {e.Message}");
            e.Handled = true;
        };

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}

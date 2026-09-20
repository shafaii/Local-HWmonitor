using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;
using PcSentinel.Infrastructure.Persistence;

namespace PcSentinel.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IHardwareMonitorService _monitorService;
    private readonly ITelemetryRepository _repository;
    private readonly TelemetryRetentionWorker? _retentionWorker;

    public string Title => "Telemetry Settings & Data Management";
    public string Description => "Configure telemetry polling cadence, SQLite retention policies, and storage quotas.";

    public SettingsViewModel(
        IHardwareMonitorService monitorService,
        ITelemetryRepository repository,
        TelemetryRetentionWorker? retentionWorker = null)
    {
        _monitorService = monitorService;
        _repository = repository;
        _retentionWorker = retentionWorker;

        PollingIntervalOptions = new ObservableCollection<int> { 500, 1000, 2000, 5000 };
        _selectedPollingInterval = (int)_monitorService.PollingInterval.TotalMilliseconds;

        RetentionOptions = new ObservableCollection<RetentionItem>
        {
            new(RetentionPeriod.OneDay, "1 Day"),
            new(RetentionPeriod.SevenDays, "7 Days"),
            new(RetentionPeriod.ThirtyDays, "30 Days (Default)"),
            new(RetentionPeriod.NinetyDays, "90 Days"),
            new(RetentionPeriod.Unlimited, "Unlimited")
        };

        _selectedRetention = RetentionOptions[2]; // 30 Days

        if (_repository is SqliteTelemetryRepository sqlite)
        {
            DatabasePath = sqlite.DatabasePath;
        }
        else
        {
            DatabasePath = "In-Memory / Managed";
        }
    }

    public ObservableCollection<int> PollingIntervalOptions { get; }
    public ObservableCollection<RetentionItem> RetentionOptions { get; }

    [ObservableProperty]
    private int _selectedPollingInterval;

    [ObservableProperty]
    private RetentionItem _selectedRetention;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _totalStoredReadings = "—";

    [ObservableProperty]
    private string _databaseSizeFormatted = "—";

    [ObservableProperty]
    private string _activeSessionId = "None";

    [ObservableProperty]
    private string _statusMessage = "Configuration loaded.";

    [ObservableProperty]
    private bool _isPurging;

    public async Task InitializeAsync()
    {
        ActiveSessionId = _monitorService.ActiveSessionId ?? "None";
        await RefreshStorageStatsAsync();
    }

    partial void OnSelectedPollingIntervalChanged(int value)
    {
        _monitorService.PollingInterval = TimeSpan.FromMilliseconds(value);
        StatusMessage = $"Hardware polling cadence updated to {value}ms.";
    }

    partial void OnSelectedRetentionChanged(RetentionItem value)
    {
        if (_retentionWorker != null)
        {
            _retentionWorker.CurrentRetentionPeriod = value.Period;
        }
        StatusMessage = $"Data retention policy updated to {value.DisplayName}.";
    }

    [RelayCommand]
    public async Task RefreshStorageStatsAsync()
    {
        try
        {
            var stats = await _repository.GetStorageStatisticsAsync();
            TotalStoredReadings = $"{stats.TotalReadings:N0} samples";

            if (stats.DatabaseSizeBytes < 1024)
                DatabaseSizeFormatted = $"{stats.DatabaseSizeBytes} B";
            else if (stats.DatabaseSizeBytes < 1024 * 1024)
                DatabaseSizeFormatted = $"{stats.DatabaseSizeBytes / 1024.0:0.#} KB";
            else
                DatabaseSizeFormatted = $"{stats.DatabaseSizeBytes / (1024.0 * 1024.0):0.##} MB";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to read storage statistics: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PurgeExpiredDataAsync()
    {
        if (_retentionWorker == null) return;

        IsPurging = true;
        StatusMessage = "Purging expired telemetry records...";

        try
        {
            int deleted = await _retentionWorker.ExecutePurgeAsync();
            StatusMessage = deleted > 0
                ? $"Purge complete: {deleted:N0} expired records removed."
                : "Purge complete: No expired records met the deletion threshold.";

            await RefreshStorageStatsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Retention purge error: {ex.Message}";
        }
        finally
        {
            IsPurging = false;
        }
    }
}

public sealed record RetentionItem(RetentionPeriod Period, string DisplayName);

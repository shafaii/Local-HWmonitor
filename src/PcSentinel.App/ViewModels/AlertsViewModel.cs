using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.App.ViewModels;

public sealed partial class AlertsViewModel : ObservableObject
{
    private readonly IHardwareMonitorService _monitorService;
    private readonly ITrendAnalyzer? _trendAnalyzer;

    public string Title => "Telemetry Observations & Threshold Alerts";
    public string Description => "Deterministic trend observations: sustained thermal conditions, high utilization, and throttling indicators.";

    public AlertsViewModel(
        IHardwareMonitorService monitorService,
        ITrendAnalyzer? trendAnalyzer = null)
    {
        _monitorService = monitorService;
        _trendAnalyzer = trendAnalyzer;

        _monitorService.ObservationsDetected += OnObservationsDetected;
        RefreshList();
    }

    public ObservableCollection<TelemetryObservation> Observations { get; } = new();

    [ObservableProperty]
    private int _observationCount;

    [ObservableProperty]
    private string _statusSummary = "No anomalous conditions detected.";

    private void OnObservationsDetected(object? sender, System.Collections.Generic.IReadOnlyList<TelemetryObservation> newObs)
    {
        RefreshList();
    }

    [RelayCommand]
    public void RefreshList()
    {
        var list = _trendAnalyzer?.RecentObservations ?? _monitorService.LatestObservations;

        Observations.Clear();
        foreach (var obs in list.OrderByDescending(o => o.Timestamp))
        {
            Observations.Add(obs);
        }

        ObservationCount = Observations.Count;

        if (Observations.Any(o => o.Severity == ObservationSeverity.Critical))
        {
            StatusSummary = "CRITICAL: Severe thermal or throttling condition observed.";
        }
        else if (Observations.Any(o => o.Severity == ObservationSeverity.Warning))
        {
            StatusSummary = "WARNING: Elevated temperatures or rapid thermal escalation active.";
        }
        else if (Observations.Count > 0)
        {
            StatusSummary = "INFO: Sustained hardware utilization recorded.";
        }
        else
        {
            StatusSummary = "All monitored hardware operating within nominal parameters.";
        }
    }

    [RelayCommand]
    public void ClearObservations()
    {
        _trendAnalyzer?.Reset();
        Observations.Clear();
        ObservationCount = 0;
        StatusSummary = "Observation log cleared.";
    }
}

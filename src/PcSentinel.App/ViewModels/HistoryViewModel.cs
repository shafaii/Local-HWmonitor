using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.App.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoricalQueryService _queryService;
    private readonly ITelemetryRepository _repository;
    private CancellationTokenSource? _queryCts;

    public string Title => "Telemetry History & Downsampled Trends";
    public string Description => "SQLite historical telemetry inspection with automated downsampling and statistical summaries.";

    public HistoryViewModel(
        IHistoricalQueryService queryService,
        ITelemetryRepository repository)
    {
        _queryService = queryService;
        _repository = repository;

        // Initialize time range options
        TimeRangeOptions = new ObservableCollection<TimeRangeItem>
        {
            new(TimeRange.FiveMinutes, "5 Minutes (Raw)"),
            new(TimeRange.ThirtyMinutes, "30 Minutes (5s)"),
            new(TimeRange.OneHour, "1 Hour (10s)"),
            new(TimeRange.SixHours, "6 Hours (30s)"),
            new(TimeRange.TwentyFourHours, "24 Hours (1m)"),
            new(TimeRange.SevenDays, "7 Days (10m)"),
            new(TimeRange.ThirtyDays, "30 Days (1h)")
        };

        SelectedTimeRangeItem = TimeRangeOptions[2]; // Default to 1 Hour
    }

    public ObservableCollection<HardwareDeviceRecord> Devices { get; } = new();
    public ObservableCollection<SensorDefinitionRecord> Sensors { get; } = new();
    public ObservableCollection<TimeRangeItem> TimeRangeOptions { get; }

    [ObservableProperty]
    private HardwareDeviceRecord? _selectedDevice;

    [ObservableProperty]
    private SensorDefinitionRecord? _selectedSensor;

    [ObservableProperty]
    private TimeRangeItem _selectedTimeRangeItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _summaryCurrent = "—";

    [ObservableProperty]
    private string _summaryAverage = "—";

    [ObservableProperty]
    private string _summaryMin = "—";

    [ObservableProperty]
    private string _summaryMax = "—";

    [ObservableProperty]
    private string _sampleCountText = "No data loaded";

    [ObservableProperty]
    private string _graphPoints = string.Empty;

    [ObservableProperty]
    private string _graphAreaPoints = string.Empty;

    [ObservableProperty]
    private string _yAxisTop = "100";

    [ObservableProperty]
    private string _yAxisMid = "50";

    [ObservableProperty]
    private string _yAxisBottom = "0";

    [ObservableProperty]
    private string _xAxisStart = "—";

    [ObservableProperty]
    private string _xAxisEnd = "Now";

    [ObservableProperty]
    private string _activeUnit = string.Empty;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private string _statusMessage = "Select a sensor and time range to query telemetry history.";

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading registered hardware devices from SQLite...";

        try
        {
            var devices = await _queryService.GetAvailableHardwareAsync();
            Devices.Clear();
            foreach (var d in devices)
            {
                Devices.Add(d);
            }

            if (Devices.Count > 0 && SelectedDevice == null)
            {
                SelectedDevice = Devices.FirstOrDefault(d => d.HardwareType == HardwareType.Cpu) ?? Devices[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load hardware devices: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    async partial void OnSelectedDeviceChanged(HardwareDeviceRecord? value)
    {
        if (value == null)
        {
            Sensors.Clear();
            SelectedSensor = null;
            return;
        }

        IsLoading = true;
        try
        {
            var sensors = await _queryService.GetSensorsForHardwareAsync(value.Id);
            Sensors.Clear();
            foreach (var s in sensors)
            {
                Sensors.Add(s);
            }

            if (Sensors.Count > 0)
            {
                // Prefer temperature or load sensor by default
                SelectedSensor = Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)
                              ?? Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load)
                              ?? Sensors[0];
            }
            else
            {
                SelectedSensor = null;
                ResetGraph();
                StatusMessage = "No sensors recorded for this device yet.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading sensors: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    async partial void OnSelectedSensorChanged(SensorDefinitionRecord? value)
    {
        if (value != null)
        {
            ActiveUnit = value.Unit;
            await RefreshDataAsync();
        }
        else
        {
            ResetGraph();
        }
    }

    async partial void OnSelectedTimeRangeItemChanged(TimeRangeItem value)
    {
        if (SelectedSensor != null)
        {
            await RefreshDataAsync();
        }
    }

    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        if (SelectedSensor == null) return;

        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var ct = _queryCts.Token;

        IsLoading = true;
        StatusMessage = "Executing downsampling query on SQLite...";

        try
        {
            var data = await _queryService.QuerySensorDataAsync(
                SelectedSensor.Id,
                SelectedTimeRangeItem.Range,
                ct);

            if (data.Count == 0)
            {
                ResetGraph();
                StatusMessage = $"No historical telemetry records found for {SelectedSensor.Name} within {SelectedTimeRangeItem.DisplayName}.";
                return;
            }

            HasData = true;
            RenderGraph(data, SelectedSensor.Unit);
            StatusMessage = $"Loaded {data.Count} downsampled buckets successfully.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Historical query error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RenderGraph(IReadOnlyList<AggregatedReading> data, string unit)
    {
        double minVal = data.Min(d => d.Min);
        double maxVal = data.Max(d => d.Max);
        double avgVal = data.Average(d => d.Average);
        double currentVal = data.Last().Average;

        SummaryMin = $"{minVal:0.#} {unit}".Trim();
        SummaryMax = $"{maxVal:0.#} {unit}".Trim();
        SummaryAverage = $"{avgVal:0.#} {unit}".Trim();
        SummaryCurrent = $"{currentVal:0.#} {unit}".Trim();

        int totalRawCount = data.Sum(d => d.SampleCount);
        SampleCountText = $"{data.Count} aggregated points ({totalRawCount:N0} raw samples)";

        // Graph canvas coordinate dimensions
        const double width = 800.0;
        const double height = 260.0;
        const double padY = 20.0;

        // Auto-scale Y axis with breathing headroom
        double yRange = maxVal - minVal;
        if (yRange < 1.0) yRange = 1.0;

        double yMin = Math.Floor(minVal - (yRange * 0.1));
        double yMax = Math.Ceiling(maxVal + (yRange * 0.1));
        if (yMin < 0 && minVal >= 0) yMin = 0; // Clamp to 0 if all readings are positive

        double ySpan = yMax - yMin;
        if (ySpan <= 0) ySpan = 1.0;

        YAxisTop = $"{yMax:0.#} {unit}".Trim();
        YAxisMid = $"{(yMin + (ySpan / 2.0)):0.#} {unit}".Trim();
        YAxisBottom = $"{yMin:0.#} {unit}".Trim();

        XAxisStart = data.First().TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        XAxisEnd = data.Last().TimestampUtc.ToLocalTime().ToString("HH:mm:ss");

        var sbPoints = new StringBuilder();
        var sbArea = new StringBuilder();

        // Start area at bottom left
        sbArea.Append(CultureInfo.InvariantCulture, $"0,{height} ");

        for (int i = 0; i < data.Count; i++)
        {
            double x = data.Count == 1 ? (width / 2.0) : (i / (double)(data.Count - 1)) * width;
            double normY = (data[i].Average - yMin) / ySpan;
            double y = height - padY - (normY * (height - (2 * padY)));

            sbPoints.Append(CultureInfo.InvariantCulture, $"{x:0.#},{y:0.#} ");
            sbArea.Append(CultureInfo.InvariantCulture, $"{x:0.#},{y:0.#} ");
        }

        // Close area polygon at bottom right
        sbArea.Append(CultureInfo.InvariantCulture, $"{width},{height}");

        GraphPoints = sbPoints.ToString().TrimEnd();
        GraphAreaPoints = sbArea.ToString().TrimEnd();
    }

    private void ResetGraph()
    {
        HasData = false;
        GraphPoints = string.Empty;
        GraphAreaPoints = string.Empty;
        SummaryCurrent = "—";
        SummaryAverage = "—";
        SummaryMin = "—";
        SummaryMax = "—";
        SampleCountText = "No data loaded";
    }
}

public sealed record TimeRangeItem(TimeRange Range, string DisplayName);

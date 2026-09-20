using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using PcSentinel.Core.Models;

namespace PcSentinel.App.ViewModels;

public sealed partial class HardwareCardViewModel : ObservableObject
{
    private readonly Queue<double> _history = new();
    private const int MaxHistory = 30;
    private const double SparklineWidth = 140.0;
    private const double SparklineHeight = 36.0;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private HardwareType _hardwareType;

    [ObservableProperty]
    private double _primaryPercentage;

    [ObservableProperty]
    private string _primaryPercentageFormatted = "0%";

    [ObservableProperty]
    private string _temperatureText = "—";

    [ObservableProperty]
    private string _clockText = "—";

    [ObservableProperty]
    private string _powerOrCapacityText = "—";

    [ObservableProperty]
    private string _statusTag = "Nominal";

    [ObservableProperty]
    private string _statusColor = "#107C41"; // Windows Fluent Green

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private string _sparklinePoints = string.Empty;

    [ObservableProperty]
    private string _sparklineMinMaxText = string.Empty;

    public void AddSparklineSample(double value)
    {
        _history.Enqueue(value);
        while (_history.Count > MaxHistory)
        {
            _history.Dequeue();
        }

        if (_history.Count < 2) return;

        var array = _history.ToArray();
        double min = array.Min();
        double max = array.Max();
        double span = max - min;
        if (span < 1.0) span = 1.0;

        SparklineMinMaxText = $"{min:0.#} - {max:0.#}%";

        var sb = new StringBuilder();
        for (int i = 0; i < array.Length; i++)
        {
            double x = (i / (double)(array.Length - 1)) * SparklineWidth;
            double normY = (array[i] - min) / span;
            double y = SparklineHeight - (normY * (SparklineHeight - 6.0)) - 3.0; // padding

            sb.Append(CultureInfo.InvariantCulture, $"{x:0.#},{y:0.#} ");
        }

        SparklinePoints = sb.ToString().TrimEnd();
    }
}

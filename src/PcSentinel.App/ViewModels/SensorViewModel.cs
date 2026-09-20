using CommunityToolkit.Mvvm.ComponentModel;
using PcSentinel.Core.Models;

namespace PcSentinel.App.ViewModels;

public sealed partial class SensorViewModel : ObservableObject
{
    private readonly Sensor _model;

    public SensorViewModel(Sensor model)
    {
        _model = model;
        UpdateFromModel(model);
    }

    [ObservableProperty]
    private string _sensorId = string.Empty;

    [ObservableProperty]
    private string _sensorName = string.Empty;

    [ObservableProperty]
    private SensorType _sensorType = SensorType.Unknown;

    [ObservableProperty]
    private string _hardwareName = string.Empty;

    [ObservableProperty]
    private double? _value;

    [ObservableProperty]
    private double? _min;

    [ObservableProperty]
    private double? _max;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _formattedValue = "—";

    [ObservableProperty]
    private string _formattedMin = "—";

    [ObservableProperty]
    private string _formattedMax = "—";

    [ObservableProperty]
    private bool _requiresElevation;

    public void UpdateFromModel(Sensor model)
    {
        SensorId = model.SensorId;
        SensorName = model.SensorName;
        SensorType = model.SensorType;
        HardwareName = model.HardwareName;
        Value = model.Value;
        Min = model.Min;
        Max = model.Max;
        Unit = model.Unit;
        FormattedValue = model.FormattedValue;
        FormattedMin = model.FormattedMin;
        FormattedMax = model.FormattedMax;
        RequiresElevation = model.RequiresElevation;
    }
}

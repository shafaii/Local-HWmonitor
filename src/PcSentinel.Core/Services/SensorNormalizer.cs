using System;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Services;

/// <summary>
/// Normalizes raw sensor readings into standardized units, boundary-checked ranges, and display labels.
/// </summary>
public static class SensorNormalizer
{
    public static string GetDefaultUnit(SensorType type) => type switch
    {
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Power => "W",
        SensorType.Clock => "MHz",
        SensorType.Temperature => "°C",
        SensorType.Load => "%",
        SensorType.Frequency => "Hz",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Control => "%",
        SensorType.Level => "%",
        SensorType.Factor => "",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "MB/s",
        SensorType.Energy => "mWh",
        SensorType.Noise => "dBA",
        SensorType.Humidity => "%",
        _ => string.Empty
    };

    public static double? NormalizeValue(SensorType type, double? rawValue)
    {
        if (!rawValue.HasValue || double.IsNaN(rawValue.Value) || double.IsInfinity(rawValue.Value))
        {
            return null;
        }

        double val = rawValue.Value;

        // Sensible guard-rails to prevent driver garbage
        switch (type)
        {
            case SensorType.Temperature:
                // Temperatures outside -50°C to 200°C are bogus sensor glitches
                if (val < -50 || val > 200) return null;
                break;
            case SensorType.Load:
            case SensorType.Control:
            case SensorType.Level:
                // Clamp load percentages to 0-100%
                val = Math.Clamp(val, 0.0, 100.0);
                break;
            case SensorType.Fan:
                // Negative RPM is invalid
                if (val < 0) return 0;
                break;
            case SensorType.Voltage:
                if (val < 0 || val > 50) return null;
                break;
        }

        return Math.Round(val, 2);
    }
}

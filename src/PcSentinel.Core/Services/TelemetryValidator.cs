using System;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Services;

/// <summary>
/// Validates incoming raw sensor readings against physical limits and numerical constraints.
/// Preserves legitimate high readings while rejecting physically impossible driver garbage.
/// </summary>
public static class TelemetryValidator
{
    public static TelemetryValidationResult Validate(SensorType type, double? rawValue)
    {
        if (!rawValue.HasValue)
        {
            return TelemetryValidationResult.Rejected(null, TelemetryQuality.Unavailable, "Sensor reading is null or unavailable.");
        }

        double val = rawValue.Value;

        if (double.IsNaN(val))
        {
            return TelemetryValidationResult.Rejected(val, TelemetryQuality.InvalidNumber, "Value is NaN.");
        }

        if (double.IsInfinity(val))
        {
            return TelemetryValidationResult.Rejected(val, TelemetryQuality.InvalidNumber, "Value is Infinity.");
        }

        switch (type)
        {
            case SensorType.Temperature:
                // Temperatures below -50°C or above 150°C for standard silicon are physical impossibility or open circuit
                if (val < -50.0 || val > 150.0)
                {
                    return TelemetryValidationResult.Rejected(val, TelemetryQuality.PhysicalRangeViolation, $"Temperature {val}°C outside plausible physical range (-50°C to 150°C).");
                }
                // High temperature (e.g. 100°C - 115°C) is valid silicon thermal throttle territory — do NOT clamp or reject!
                break;

            case SensorType.Load:
            case SensorType.Control:
            case SensorType.Level:
                if (val < 0.0 || val > 100.0)
                {
                    // Minor floating point overshoot like 100.1% can be capped; massive deviation rejected
                    if (val is >= -0.5 and <= 100.5)
                    {
                        return TelemetryValidationResult.Success(Math.Clamp(val, 0.0, 100.0));
                    }
                    return TelemetryValidationResult.Rejected(val, TelemetryQuality.PhysicalRangeViolation, $"Percentage load {val}% is out of range [0, 100].");
                }
                break;

            case SensorType.Fan:
                if (val < 0.0)
                {
                    return TelemetryValidationResult.Rejected(val, TelemetryQuality.PhysicalRangeViolation, "Fan RPM cannot be negative.");
                }
                if (val > 30000.0) // Extreme industrial server fans max out ~25,000 RPM
                {
                    return TelemetryValidationResult.Flagged(val, TelemetryQuality.SuspectSpike, $"Unusually high fan speed {val} RPM.");
                }
                break;

            case SensorType.Clock:
                if (val < 0.0 || val > 15000.0) // Clocks over 15GHz are bogus
                {
                    return TelemetryValidationResult.Rejected(val, TelemetryQuality.PhysicalRangeViolation, $"Clock frequency {val} MHz is out of plausible silicon bounds.");
                }
                break;

            case SensorType.Voltage:
                if (val < 0.0 || val > 48.0) // Standard PC motherboard rails never exceed 48V (12V, 5V, 3.3V, Vcore ~1V)
                {
                    return TelemetryValidationResult.Rejected(val, TelemetryQuality.PhysicalRangeViolation, $"Voltage {val} V is out of standard PC rail range.");
                }
                break;

            case SensorType.Power:
                if (val < 0.0 || val > 2000.0) // PC components do not exceed 2000W individual package power
                {
                    return TelemetryValidationResult.Rejected(val, TelemetryQuality.PhysicalRangeViolation, $"Power consumption {val} W is out of plausible bounds.");
                }
                break;
        }

        return TelemetryValidationResult.Success(Math.Round(val, 2));
    }
}

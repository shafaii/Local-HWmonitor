using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;

namespace PcSentinel.Core.Services;

public sealed class TrendAnalysisOptions
{
    public double CpuTempWarningThresholdC { get; set; } = 85.0;
    public double CpuTempCriticalThresholdC { get; set; } = 95.0;
    public double GpuTempWarningThresholdC { get; set; } = 83.0;
    public double GpuTempCriticalThresholdC { get; set; } = 90.0;
    public double SustainedHighLoadThresholdPercent { get; set; } = 90.0;
    public double SustainedDurationSeconds { get; set; } = 20.0;
    public double RapidTempIncreaseRatePerSec { get; set; } = 4.0; // >= 4°C per sec
    public int WindowSampleCapacity { get; set; } = 60;
}

/// <summary>
/// Deterministic trend analysis engine. Tracks sliding windows of telemetry and flags
/// sustained high temperatures, high utilization, rapid thermal spikes, and throttling.
/// </summary>
public sealed class TrendAnalyzer : ITrendAnalyzer
{
    private readonly TrendAnalysisOptions _options;
    private readonly ConcurrentDictionary<string, TelemetryWindow> _windows = new();
    private readonly List<TelemetryObservation> _recentObservations = new();
    private readonly object _observationsLock = new();

    // Map to remember previous clock frequencies for throttling detection
    private readonly ConcurrentDictionary<string, double> _lastClockFreq = new();

    public IReadOnlyList<TelemetryObservation> RecentObservations
    {
        get
        {
            lock (_observationsLock)
            {
                return _recentObservations.ToList();
            }
        }
    }

    public TrendAnalyzer(TrendAnalysisOptions? options = null)
    {
        _options = options ?? new TrendAnalysisOptions();
    }

    public IReadOnlyList<TelemetryObservation> AnalyzeSnapshot(HardwareSnapshot snapshot)
    {
        var newObservations = new List<TelemetryObservation>();
        var now = snapshot.Timestamp;

        foreach (var hw in snapshot.Hardware)
        {
            AnalyzeHardwareRecursive(hw, now, newObservations);
        }

        lock (_observationsLock)
        {
            _recentObservations.AddRange(newObservations);
            // Cap stored observations to last 100
            while (_recentObservations.Count > 100)
            {
                _recentObservations.RemoveAt(0);
            }
        }

        return newObservations;
    }

    private void AnalyzeHardwareRecursive(HardwareItem hw, DateTimeOffset timestamp, List<TelemetryObservation> observations)
    {
        Sensor? tempSensor = null;
        Sensor? clockSensor = null;

        foreach (var sensor in hw.Sensors)
        {
            if (!sensor.Value.HasValue) continue;

            double val = sensor.Value.Value;
            var window = _windows.GetOrAdd(sensor.SensorId, _ => new TelemetryWindow(_options.WindowSampleCapacity));
            window.AddSample(timestamp, val);

            if (sensor.SensorType == SensorType.Temperature)
            {
                tempSensor = sensor;
                AnalyzeTemperature(hw, sensor, window, observations);
            }
            else if (sensor.SensorType == SensorType.Load)
            {
                AnalyzeLoad(hw, sensor, window, observations);
            }
            else if (sensor.SensorType == SensorType.Clock)
            {
                clockSensor = sensor;
            }
        }

        // Correlate thermal throttling: High temperature + sudden clock frequency drop
        if (tempSensor?.Value != null && clockSensor?.Value != null)
        {
            CheckThermalThrottling(hw, tempSensor, clockSensor, observations);
        }

        foreach (var sub in hw.SubHardware)
        {
            AnalyzeHardwareRecursive(sub, timestamp, observations);
        }
    }

    private void AnalyzeTemperature(HardwareItem hw, Sensor sensor, TelemetryWindow window, List<TelemetryObservation> observations)
    {
        double current = sensor.Value!.Value;
        bool isCpu = hw.Type == HardwareType.Cpu;
        bool isGpu = hw.Type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

        double warnThresh = isCpu ? _options.CpuTempWarningThresholdC : (isGpu ? _options.GpuTempWarningThresholdC : 80.0);
        double critThresh = isCpu ? _options.CpuTempCriticalThresholdC : (isGpu ? _options.GpuTempCriticalThresholdC : 90.0);

        // 1. Immediate critical temperature check
        if (current >= critThresh)
        {
            observations.Add(new TelemetryObservation
            {
                Type = ObservationType.HighTemperature,
                Severity = ObservationSeverity.Critical,
                HardwareId = hw.Id,
                SensorId = sensor.SensorId,
                HardwareName = hw.Name,
                SensorName = sensor.SensorName,
                CurrentValue = current,
                Threshold = critThresh,
                DurationSeconds = null,
                Unit = sensor.Unit,
                Message = $"High CPU temperature critical limit: {current:0.#}°C on {sensor.SensorName} exceeds {critThresh:0.#}°C"
            });
        }
        else
        {
            // 2. Sustained high temperature check
            double sustainedSec = window.SustainedDurationAbove(warnThresh);
            if (sustainedSec >= _options.SustainedDurationSeconds)
            {
                observations.Add(new TelemetryObservation
                {
                    Type = ObservationType.HighTemperature,
                    Severity = ObservationSeverity.Warning,
                    HardwareId = hw.Id,
                    SensorId = sensor.SensorId,
                    HardwareName = hw.Name,
                    SensorName = sensor.SensorName,
                    CurrentValue = current,
                    Threshold = warnThresh,
                    DurationSeconds = Math.Round(sustainedSec, 1),
                    Unit = sensor.Unit,
                    Message = $"Sustained high temperature above {warnThresh:0.#}°C for {sustainedSec:0.#}s (Current: {current:0.#}°C)"
                });
            }
        }

        // 3. Rapid temperature increase rate check (dV/dt)
        double? rate = window.RateOfChangePerSecond();
        if (rate.HasValue && rate.Value >= _options.RapidTempIncreaseRatePerSec)
        {
            observations.Add(new TelemetryObservation
            {
                Type = ObservationType.RapidTemperatureIncrease,
                Severity = ObservationSeverity.Warning,
                HardwareId = hw.Id,
                SensorId = sensor.SensorId,
                HardwareName = hw.Name,
                SensorName = sensor.SensorName,
                CurrentValue = current,
                Threshold = _options.RapidTempIncreaseRatePerSec,
                DurationSeconds = null,
                Unit = "°C/s",
                Message = $"Rapid thermal escalation detected: +{rate.Value:0.#}°C/s on {sensor.SensorName} (Current: {current:0.#}°C)"
            });
        }
    }

    private void AnalyzeLoad(HardwareItem hw, Sensor sensor, TelemetryWindow window, List<TelemetryObservation> observations)
    {
        double current = sensor.Value!.Value;
        double sustainedSec = window.SustainedDurationAbove(_options.SustainedHighLoadThresholdPercent);

        if (sustainedSec >= _options.SustainedDurationSeconds)
        {
            observations.Add(new TelemetryObservation
            {
                Type = ObservationType.SustainedHighUtilization,
                Severity = ObservationSeverity.Info,
                HardwareId = hw.Id,
                SensorId = sensor.SensorId,
                HardwareName = hw.Name,
                SensorName = sensor.SensorName,
                CurrentValue = current,
                Threshold = _options.SustainedHighLoadThresholdPercent,
                DurationSeconds = Math.Round(sustainedSec, 1),
                Unit = sensor.Unit,
                Message = $"Sustained high utilization above {_options.SustainedHighLoadThresholdPercent:0.#}% for {sustainedSec:0.#}s"
            });
        }
    }

    private void CheckThermalThrottling(HardwareItem hw, Sensor tempSensor, Sensor clockSensor, List<TelemetryObservation> observations)
    {
        double temp = tempSensor.Value!.Value;
        double clock = clockSensor.Value!.Value;

        if (_lastClockFreq.TryGetValue(clockSensor.SensorId, out double prevClock))
        {
            // If temperature is critically high (e.g. >= 90°C for CPU) and clock suddenly drops by >= 400 MHz
            bool isHighTemp = temp >= (_options.CpuTempWarningThresholdC + 5.0);
            double clockDrop = prevClock - clock;

            if (isHighTemp && clockDrop >= 400.0)
            {
                observations.Add(new TelemetryObservation
                {
                    Type = ObservationType.PossibleThermalThrottling,
                    Severity = ObservationSeverity.Critical,
                    HardwareId = hw.Id,
                    SensorId = clockSensor.SensorId,
                    HardwareName = hw.Name,
                    SensorName = clockSensor.SensorName,
                    CurrentValue = clock,
                    Threshold = prevClock,
                    DurationSeconds = null,
                    Unit = "MHz",
                    Message = $"Possible thermal throttling detected on {hw.Name}: Clock throttled by {clockDrop:0.#} MHz while temperature reached {temp:0.#}°C"
                });
            }
        }

        _lastClockFreq[clockSensor.SensorId] = clock;
    }

    public void Reset()
    {
        _windows.Clear();
        _lastClockFreq.Clear();
        lock (_observationsLock)
        {
            _recentObservations.Clear();
        }
    }
}

namespace PcSentinel.Core.Models;

public enum HealthSeverity
{
    Normal,
    Elevated,
    Warning,
    Critical
}

public sealed class SystemHealthState
{
    public HealthSeverity OverallSeverity { get; set; } = HealthSeverity.Normal;
    public double? CpuMaxTemperature { get; set; }
    public double? GpuMaxTemperature { get; set; }
    public double? CpuTotalLoad { get; set; }
    public double? MemoryUtilization { get; set; }
    public bool IsCpuThrottling { get; set; }
    public bool IsGpuThrottling { get; set; }
    public List<string> HealthNotices { get; set; } = new();

    public static SystemHealthState Evaluate(HardwareSnapshot snapshot)
    {
        var state = new SystemHealthState();

        // CPU evaluation
        var cpuTemps = snapshot.Cpu?.Sensors
            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
            .Select(s => s.Value!.Value)
            .ToList();

        if (cpuTemps != null && cpuTemps.Count > 0)
        {
            state.CpuMaxTemperature = cpuTemps.Max();
            if (state.CpuMaxTemperature >= 95.0)
            {
                state.OverallSeverity = HealthSeverity.Critical;
                state.IsCpuThrottling = true;
                state.HealthNotices.Add($"CPU temperature critical ({state.CpuMaxTemperature:0.#}°C). Thermal throttling likely.");
            }
            else if (state.CpuMaxTemperature >= 85.0)
            {
                state.OverallSeverity = HealthSeverity.Warning;
                state.HealthNotices.Add($"CPU temperature elevated ({state.CpuMaxTemperature:0.#}°C).");
            }
        }

        var cpuLoad = snapshot.Cpu?.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Total", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        state.CpuTotalLoad = cpuLoad;

        // GPU evaluation
        var gpuTemps = snapshot.Gpu?.Sensors
            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
            .Select(s => s.Value!.Value)
            .ToList();

        if (gpuTemps != null && gpuTemps.Count > 0)
        {
            state.GpuMaxTemperature = gpuTemps.Max();
            if (state.GpuMaxTemperature >= 90.0)
            {
                if (state.OverallSeverity < HealthSeverity.Critical) state.OverallSeverity = HealthSeverity.Critical;
                state.IsGpuThrottling = true;
                state.HealthNotices.Add($"GPU temperature critical ({state.GpuMaxTemperature:0.#}°C). Check cooling fans.");
            }
            else if (state.GpuMaxTemperature >= 82.0 && state.OverallSeverity < HealthSeverity.Warning)
            {
                state.OverallSeverity = HealthSeverity.Warning;
                state.HealthNotices.Add($"GPU temperature warm ({state.GpuMaxTemperature:0.#}°C).");
            }
        }

        // Memory evaluation
        var memLoad = snapshot.Memory?.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Load && s.SensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        state.MemoryUtilization = memLoad;
        if (memLoad >= 95.0)
        {
            if (state.OverallSeverity < HealthSeverity.Warning) state.OverallSeverity = HealthSeverity.Warning;
            state.HealthNotices.Add($"Memory utilization critically high ({memLoad:0.#}%).");
        }

        if (state.HealthNotices.Count == 0)
        {
            state.HealthNotices.Add("All hardware sensors operating within nominal thermal and electrical specifications.");
        }

        return state;
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using LibreHardwareMonitor.Hardware;
using PcSentinel.Core.Interfaces;
using PcSentinel.Core.Models;
using PcSentinel.Core.Services;
using Lhm = LibreHardwareMonitor.Hardware;

namespace PcSentinel.Hardware.LibreHardware;

/// <summary>
/// Hardware provider utilizing LibreHardwareMonitorLib to inspect physical and logical hardware.
/// </summary>
public sealed class LibreHardwareMonitorProvider : IHardwareProvider
{
    private readonly ILogger<LibreHardwareMonitorProvider>? _logger;
    private readonly LibreHardwareVisitor _visitor = new();
    private Computer? _computer;
    private bool _isDisposed;
    private bool _isInitialized;

    public string ProviderName => "LibreHardwareMonitorLib";
    public bool IsAvailable { get; private set; }
    public bool RequiresAdministrator { get; private set; }

    public LibreHardwareMonitorProvider(ILogger<LibreHardwareMonitorProvider>? logger = null)
    {
        _logger = logger;
        RequiresAdministrator = !CheckIsAdministrator();
    }

    private static bool CheckIsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            _logger?.LogInformation("Initializing LibreHardwareMonitor Computer instance...");
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };

            _computer.Open();
            IsAvailable = true;
            _isInitialized = true;
            _logger?.LogInformation("LibreHardwareMonitor initialized successfully. Administrator: {IsAdmin}", !RequiresAdministrator);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize LibreHardwareMonitorLib: {Message}. Operating in graceful degradation mode.", ex.Message);
            IsAvailable = false;
        }
    }

    public IReadOnlyList<HardwareItem> PollHardware()
    {
        if (!_isInitialized || _computer == null || !IsAvailable)
        {
            return Array.Empty<HardwareItem>();
        }

        try
        {
            _computer.Accept(_visitor);

            var result = new List<HardwareItem>();
            foreach (var lhmHardware in _computer.Hardware)
            {
                result.Add(MapHardwareRecursive(lhmHardware));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error polling hardware sensors via LibreHardwareMonitorLib: {Message}", ex.Message);
            return Array.Empty<HardwareItem>();
        }
    }

    private HardwareItem MapHardwareRecursive(IHardware lhmHw)
    {
        var item = new HardwareItem
        {
            Id = lhmHw.Identifier.ToString(),
            Name = lhmHw.Name,
            Type = LibreHardwareMapper.MapHardwareType(lhmHw.HardwareType),
            Identifier = lhmHw.Identifier.ToString()
        };

        foreach (var lhmSensor in lhmHw.Sensors)
        {
            var sensorType = LibreHardwareMapper.MapSensorType(lhmSensor.SensorType);
            var normalizedVal = SensorNormalizer.NormalizeValue(sensorType, lhmSensor.Value);
            var normalizedMin = SensorNormalizer.NormalizeValue(sensorType, lhmSensor.Min);
            var normalizedMax = SensorNormalizer.NormalizeValue(sensorType, lhmSensor.Max);

            item.Sensors.Add(new Sensor
            {
                SensorId = lhmSensor.Identifier.ToString(),
                SensorName = lhmSensor.Name,
                SensorType = sensorType,
                HardwareId = item.Id,
                HardwareName = item.Name,
                HardwareType = item.Type,
                Value = normalizedVal,
                Min = normalizedMin,
                Max = normalizedMax,
                Unit = SensorNormalizer.GetDefaultUnit(sensorType),
                Timestamp = DateTimeOffset.UtcNow,
                RequiresElevation = RequiresAdministrator && (sensorType is PcSentinel.Core.Models.SensorType.Temperature or PcSentinel.Core.Models.SensorType.Power or PcSentinel.Core.Models.SensorType.Voltage)
            });
        }

        foreach (var sub in lhmHw.SubHardware)
        {
            item.SubHardware.Add(MapHardwareRecursive(sub));
        }

        return item;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _computer?.Close();
        }
        catch
        {
            // Ignore close exceptions
        }
    }
}

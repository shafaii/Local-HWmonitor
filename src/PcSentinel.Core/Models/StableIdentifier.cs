using System;
using System.Text.RegularExpressions;

namespace PcSentinel.Core.Models;

/// <summary>
/// Utility to generate deterministic, order-independent stable identifiers
/// for hardware devices and sensors across reboots and enumeration variations.
/// </summary>
public static partial class StableIdentifier
{
    private static readonly Regex SanitizeRegex = new(@"[^a-z0-9_\-\.]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Generates a stable hardware identifier based on hardware type and canonical device key.
    /// Does NOT rely on transient memory addresses or runtime enumeration order.
    /// </summary>
    public static string CreateHardwareId(HardwareType type, string deviceName, string? rawIdentifier = null)
    {
        string typePrefix = type.ToString().ToLowerInvariant();

        // If rawIdentifier is provided and contains a structural path (e.g. /amdcpu/0 or /nvidiagpu/0),
        // normalize that path into a canonical identifier
        if (!string.IsNullOrWhiteSpace(rawIdentifier) && rawIdentifier.StartsWith('/'))
        {
            string cleanRaw = rawIdentifier.Trim('/').Replace('/', ':').ToLowerInvariant();
            return $"{typePrefix}:{cleanRaw}";
        }

        string cleanName = SanitizeRegex.Replace(deviceName.Trim().ToLowerInvariant().Replace(' ', '-'), "");
        return $"{typePrefix}:{cleanName}";
    }

    /// <summary>
    /// Generates a stable sensor identifier by combining the stable hardware identifier,
    /// sensor type, and normalized sensor name.
    /// </summary>
    public static string CreateSensorId(string stableHardwareId, SensorType sensorType, string sensorName)
    {
        string typeStr = sensorType.ToString().ToLowerInvariant();
        string cleanSensorName = SanitizeRegex.Replace(sensorName.Trim().ToLowerInvariant().Replace(' ', '-'), "");
        return $"{stableHardwareId}/{typeStr}/{cleanSensorName}";
    }
}

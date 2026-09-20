namespace PcSentinel.Core.Models;

/// <summary>
/// Normalized sensor classification across any underlying hardware monitor provider.
/// </summary>
public enum SensorType
{
    Voltage,
    Current,
    Power,
    Clock,
    Temperature,
    Load,
    Frequency,
    Fan,
    Flow,
    Control,
    Level,
    Factor,
    Data,
    SmallData,
    Throughput,
    TimeSpan,
    Energy,
    Noise,
    Humidity,
    Unknown
}

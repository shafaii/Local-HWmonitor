using PcSentinel.Core.Models;
using PcSentinel.Core.Services;
using Xunit;

namespace PcSentinel.Core.Tests;

public class SensorNormalizationTests
{
    [Theory]
    [InlineData(SensorType.Temperature, 65.456, 65.46, "°C")]
    [InlineData(SensorType.Load, 102.5, 100.0, "%")]
    [InlineData(SensorType.Load, -5.0, 0.0, "%")]
    [InlineData(SensorType.Clock, 4800.0, 4800.0, "MHz")]
    [InlineData(SensorType.Fan, 1500.2, 1500.2, "RPM")]
    [InlineData(SensorType.Fan, -50.0, 0.0, "RPM")]
    [InlineData(SensorType.Power, 125.678, 125.68, "W")]
    [InlineData(SensorType.Voltage, 1.254, 1.25, "V")]
    [InlineData(SensorType.Data, 16.4, 16.4, "GB")]
    public void NormalizeValue_ShouldClampAndRoundCorrectly(SensorType type, double raw, double expectedVal, string expectedUnit)
    {
        var normalized = SensorNormalizer.NormalizeValue(type, raw);
        var unit = SensorNormalizer.GetDefaultUnit(type);

        Assert.NotNull(normalized);
        Assert.Equal(expectedVal, normalized!.Value);
        Assert.Equal(expectedUnit, unit);
    }

    [Fact]
    public void NormalizeValue_BogusTemperatures_ShouldReturnNull()
    {
        // Negative below -50C or above 200C are bogus hardware glitches
        Assert.Null(SensorNormalizer.NormalizeValue(SensorType.Temperature, -100.0));
        Assert.Null(SensorNormalizer.NormalizeValue(SensorType.Temperature, 250.0));
        Assert.Null(SensorNormalizer.NormalizeValue(SensorType.Temperature, double.NaN));
    }

    [Fact]
    public void Sensor_FormattedProperties_ShouldFormatGracefully()
    {
        var sensor = new Sensor
        {
            SensorName = "Core #1",
            SensorType = SensorType.Temperature,
            Value = 72.4,
            Min = 41.2,
            Max = 88.0,
            Unit = "°C"
        };

        Assert.Equal("72.4 °C", sensor.FormattedValue);
        Assert.Equal("41.2 °C", sensor.FormattedMin);
        Assert.Equal("88 °C", sensor.FormattedMax);
    }
}

using System;
using PcSentinel.Core.Models;
using PcSentinel.Core.Services;
using Xunit;

namespace PcSentinel.Core.Tests;

public class TelemetryValidatorTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_RejectsNonFiniteValues(double value)
    {
        var result = TelemetryValidator.Validate(SensorType.Temperature, value);
        Assert.False(result.IsValid);
        Assert.Equal(TelemetryQuality.InvalidNumber, result.Quality);
    }

    [Theory]
    [InlineData(-20.0, true)]
    [InlineData(45.0, true)]
    [InlineData(105.0, true)]
    [InlineData(-55.0, false)] // Below -50 C
    [InlineData(160.0, false)] // Above 150 C
    public void Validate_TemperatureBounds(double value, bool expectedValid)
    {
        var result = TelemetryValidator.Validate(SensorType.Temperature, value);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(50.0, true)]
    [InlineData(100.0, true)]
    [InlineData(-1.0, false)]
    [InlineData(101.0, false)]
    public void Validate_LoadBounds(double value, bool expectedValid)
    {
        var result = TelemetryValidator.Validate(SensorType.Load, value);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1.25, true)]
    [InlineData(12.0, true)]
    [InlineData(-0.5, false)]
    [InlineData(50.0, false)] // Unrealistic voltage
    public void Validate_VoltageBounds(double value, bool expectedValid)
    {
        var result = TelemetryValidator.Validate(SensorType.Voltage, value);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1500.0, true)]
    [InlineData(8000.0, true)]
    [InlineData(-10.0, false)]
    [InlineData(35000.0, false)] // Exceeds max RPM
    public void Validate_FanBounds(double value, bool expectedValid)
    {
        var result = TelemetryValidator.Validate(SensorType.Fan, value);
        Assert.Equal(expectedValid, result.IsValid);
    }
}

using System;

namespace PcSentinel.Core.Models;

public enum TimeRange
{
    FiveMinutes,
    ThirtyMinutes,
    OneHour,
    SixHours,
    TwentyFourHours,
    SevenDays,
    ThirtyDays
}

public static class TimeRangeHelper
{
    public static TimeSpan ToTimeSpan(TimeRange range) => range switch
    {
        TimeRange.FiveMinutes => TimeSpan.FromMinutes(5),
        TimeRange.ThirtyMinutes => TimeSpan.FromMinutes(30),
        TimeRange.OneHour => TimeSpan.FromHours(1),
        TimeRange.SixHours => TimeSpan.FromHours(6),
        TimeRange.TwentyFourHours => TimeSpan.FromDays(1),
        TimeRange.SevenDays => TimeSpan.FromDays(7),
        TimeRange.ThirtyDays => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(1)
    };

    public static string ToDisplayName(TimeRange range) => range switch
    {
        TimeRange.FiveMinutes => "5 Minutes",
        TimeRange.ThirtyMinutes => "30 Minutes",
        TimeRange.OneHour => "1 Hour",
        TimeRange.SixHours => "6 Hours",
        TimeRange.TwentyFourHours => "24 Hours",
        TimeRange.SevenDays => "7 Days",
        TimeRange.ThirtyDays => "30 Days",
        _ => "1 Hour"
    };

    /// <summary>
    /// Computes the recommended downsampling aggregation bucket size for a given time range
    /// to keep charting smooth and responsive without rendering millions of points.
    /// </summary>
    public static TimeSpan GetRecommendedBucketSize(TimeRange range) => range switch
    {
        TimeRange.FiveMinutes => TimeSpan.Zero,                    // Raw 1-second data
        TimeRange.ThirtyMinutes => TimeSpan.FromSeconds(5),        // ~360 data points
        TimeRange.OneHour => TimeSpan.FromSeconds(10),             // ~360 data points
        TimeRange.SixHours => TimeSpan.FromSeconds(30),            // ~720 data points
        TimeRange.TwentyFourHours => TimeSpan.FromMinutes(1),      // ~1440 data points
        TimeRange.SevenDays => TimeSpan.FromMinutes(10),           // ~1008 data points
        TimeRange.ThirtyDays => TimeSpan.FromHours(1),             // ~720 data points
        _ => TimeSpan.FromSeconds(10)
    };
}

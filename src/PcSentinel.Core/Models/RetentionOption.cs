using System;

namespace PcSentinel.Core.Models;

public enum RetentionPeriod
{
    OneDay,
    SevenDays,
    ThirtyDays,
    NinetyDays,
    Unlimited
}

public static class RetentionHelper
{
    public static TimeSpan? ToTimeSpan(RetentionPeriod period) => period switch
    {
        RetentionPeriod.OneDay => TimeSpan.FromDays(1),
        RetentionPeriod.SevenDays => TimeSpan.FromDays(7),
        RetentionPeriod.ThirtyDays => TimeSpan.FromDays(30),
        RetentionPeriod.NinetyDays => TimeSpan.FromDays(90),
        RetentionPeriod.Unlimited => null,
        _ => TimeSpan.FromDays(30)
    };

    public static string ToDisplayName(RetentionPeriod period) => period switch
    {
        RetentionPeriod.OneDay => "1 Day",
        RetentionPeriod.SevenDays => "7 Days",
        RetentionPeriod.ThirtyDays => "30 Days (Default)",
        RetentionPeriod.NinetyDays => "90 Days",
        RetentionPeriod.Unlimited => "Unlimited",
        _ => "30 Days"
    };
}

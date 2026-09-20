using System;
using System.Collections.Generic;

namespace PcSentinel.AI.Diagnostics;

public sealed class DiagnosticReport
{
    public Guid ReportId { get; set; } = Guid.NewGuid();
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public string PrimaryBottleneck { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
    public List<string> PotentialRisks { get; set; } = new();
}

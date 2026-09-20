using CommunityToolkit.Mvvm.ComponentModel;

namespace PcSentinel.App.ViewModels;

public sealed class DiagnosticsViewModel : ObservableObject
{
    public string Title => "AI-Assisted Diagnostic Workstation";
    public string Description => "Intelligent hardware analysis and troubleshooting assistant (Phase 5). Generates compact diagnostic snapshots for Gemini.";
}

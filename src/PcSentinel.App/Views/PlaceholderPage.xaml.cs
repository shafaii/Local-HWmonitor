using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace PcSentinel.App.Views;

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string tag)
        {
            switch (tag)
            {
                case "History":
                    PageTitle.Text = "Telemetry History & Trend Analysis";
                    PageDescription.Text = "Real-time rolling buffers, SQLite historical metrics, and min/max/average sensor timeline graphs across historical monitoring sessions.";
                    PageMilestone.Text = "Phase 2 Target • Local Persistence & Time-Series Engine";
                    PageIcon.Glyph = "\uE81C"; // Clock
                    break;
                case "Diagnostics":
                    PageTitle.Text = "AI-Assisted Diagnostic Workstation";
                    PageDescription.Text = "Non-blocking hardware health evaluation, thermal anomaly detection, bottleneck identification, and privacy-respecting Gemini snapshot digests.";
                    PageMilestone.Text = "Phase 5 Target • Diagnostic Snapshot Generator & Assistant";
                    PageIcon.Glyph = "\uE9CE"; // Brain / diagnostic
                    break;
                case "Alerts":
                    PageTitle.Text = "Hardware Threshold Alerts";
                    PageDescription.Text = "Configurable high-temperature, fan-stall, and power-limit triggers with native Windows notifications.";
                    PageMilestone.Text = "Phase 3 Target • Alerting & Windows Notification Hub";
                    PageIcon.Glyph = "\uEA8F"; // Report / shield
                    break;
                case "Settings":
                    PageTitle.Text = "Settings & Privilege Management";
                    PageDescription.Text = "Telemetry polling frequency, temperature scale (°C / °F), run-at-startup preferences, and ring0 driver elevation status.";
                    PageMilestone.Text = "Phase 4 Target • Config & Privilege Escalator";
                    PageIcon.Glyph = "\uE713"; // Settings gear
                    break;
            }
        }
    }
}

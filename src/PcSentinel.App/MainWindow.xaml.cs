using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PcSentinel.App.ViewModels;
using PcSentinel.App.Views;

namespace PcSentinel.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        // Set custom titlebar if supported
        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Titlebar customization skipped: {ex.Message}");
        }

        // Default navigation
        ContentFrame.Navigate(typeof(DashboardPage));

        // Start hardware monitor service
        _ = ViewModel.InitializeAsync();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            Type targetPage = tag switch
            {
                "Dashboard" => typeof(DashboardPage),
                "Hardware" => typeof(HardwarePage),
                "History" => typeof(HistoryPage),
                "Alerts" => typeof(AlertsPage),
                "Settings" => typeof(SettingsPage),
                _ => typeof(PlaceholderPage)
            };

            ContentFrame.Navigate(targetPage, tag);
        }
    }
}

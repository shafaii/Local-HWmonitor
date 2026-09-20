using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PcSentinel.App.ViewModels;

namespace PcSentinel.App.Views;

public sealed partial class AlertsPage : Page
{
    public AlertsViewModel ViewModel { get; }

    public AlertsPage()
    {
        ViewModel = App.Services.GetRequiredService<AlertsViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshList();
    }
}

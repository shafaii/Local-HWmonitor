using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using PcSentinel.App.ViewModels;

namespace PcSentinel.App.Views;

public sealed partial class HardwarePage : Page
{
    public HardwareViewModel ViewModel { get; }

    public HardwarePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<HardwareViewModel>();
    }
}

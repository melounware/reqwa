using Microsoft.UI.Xaml.Controls;
using ReqwaColors.ViewModels;

namespace ReqwaColors.Views;

/// <summary>Settings page. Logic lives in <see cref="SettingsViewModel"/>.</summary>
public sealed partial class SettingsPage : UserControl
{
    public MainViewModel Vm { get; }

    public SettingsPage(MainViewModel vm)
    {
        Vm = vm;
        InitializeComponent();
    }
}
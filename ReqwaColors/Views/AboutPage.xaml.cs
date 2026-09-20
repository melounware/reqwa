using Microsoft.UI.Xaml.Controls;

namespace ReqwaColors.Views;

/// <summary>Static About page.</summary>
public sealed partial class AboutPage : UserControl
{
    public string VersionText { get; } = "Version 1.0.0";

    public AboutPage()
    {
        InitializeComponent();
    }
}
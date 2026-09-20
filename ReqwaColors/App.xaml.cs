using Microsoft.UI.Xaml;
using ReqwaColors.Views;

namespace ReqwaColors;

/// <summary>
/// Application entry point. Creates the shell window; all service wiring
/// lives in <see cref="ViewModels.MainViewModel"/> (single composition root).
/// </summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
        _mainWindow.HandleStartupBehavior();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services.Logger.Error("Unhandled exception", e.Exception);
        e.Handled = true;
    }
}
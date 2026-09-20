using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using ReqwaColors.Models;
using ReqwaColors.Native;
using ReqwaColors.Services;
using ReqwaColors.ViewModels;
using Windows.UI;

namespace ReqwaColors.Views;

/// <summary>
/// App shell: sidebar navigation, page host, toast notifications and
/// close-to-tray behavior.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly TrayIconService _tray = new();
    private DispatcherQueueTimer? _toastTimer;
    private bool _realExit;

    /// <summary>x:Bind source for the sidebar navigation.</summary>
    public MainViewModel ViewModel => _vm;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Reqwa Colors";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Dark native title bar buttons.
        int dark = 1;
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        AppWindow appWindow = AppWindow;
        appWindow.Title = Title;
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        // Compact fixed size (820x500 effective px, scaled for this window's DPI) - not resizable.
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = 96;
        appWindow.Resize(new Windows.Graphics.SizeInt32((int)(820 * dpi / 96.0), (int)(500 * dpi / 96.0)));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        Closed += OnClosed;

        // ---- Development diagnostics: enable verbose logging + startup diagnostics ----
        // NOTE: guarded by a compile-time symbol so this never ships in release builds.
#if DEBUG_DIAGNOSTICS
        Logger.Info("=== ReqwaColors starting (DEBUG_DIAGNOSTICS) ===");
        Logger.Info($"ProcessPath={Environment.ProcessPath}");
        Logger.Info($"args=[{string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}]");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Error("UnhandledAppDomainException", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
#endif

        _vm.Notifications.Notified += OnNotified;
        _vm.SettingsPage.ExitRequested += ExitApplication;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.Storage.Current.PropertyChanged += OnSettingsPropertyChanged;
        _vm.Initialize();

        ApplyAlwaysOnTop();

        _tray.ToggleRequested += OnHotkeyToggle;
        _tray.RegisterToggleHotkey();

        BuildPages();
        ShowPage(PageKind.Display);
    }

    /// <summary>Insert hotkey: hide to tray if visible, restore if hidden.</summary>
    private void OnHotkeyToggle()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (AppWindow.IsVisible)
                HideToTray(showNotification: false);
            else
                OnTrayOpen();
        });
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.AlwaysOnTop))
            DispatcherQueue.TryEnqueue(ApplyAlwaysOnTop);
    }

    /// <summary>Applies the always-on-top preference to the window presenter.</summary>
    private void ApplyAlwaysOnTop()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = _vm.Storage.Current.AlwaysOnTop;
    }

    /// <summary>Swaps the host content whenever the shell's current page changes.</summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            ShowPage(_vm.CurrentPage);
    }

    /// <summary>
    /// Runs after the window is activated: applies persisted colors and
    /// honors "start minimized to tray".
    /// </summary>
    public void HandleStartupBehavior()
    {
        AppSettings s = _vm.Storage.Current;

        if (s.ApplyLastColorsOnStartup)
        {
            _ = _vm.ApplySavedColorsAsync();
        }

        bool startMin = s.StartMinimized ||
            Environment.GetCommandLineArgs().Any(a =>
                string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/minimized", StringComparison.OrdinalIgnoreCase));

        if (startMin)
        {
            // Honor "notify on minimize" here too: a silent vanish to tray
            // looks exactly like "notifications don't work".
            HideToTray(showNotification: true);
        }
    }

    // =====================================================================
    // Pages
    // =====================================================================

    private DisplayPage? _displayPage;
    private PresetsPage? _presetsPage;
    private SettingsPage? _settingsPage;
    private AboutPage? _aboutPage;

    private void BuildPages()
    {
        _displayPage = new DisplayPage(_vm);
        _presetsPage = new PresetsPage(_vm);
        _settingsPage = new SettingsPage(_vm);
        _aboutPage = new AboutPage();
    }

    private void ShowPage(PageKind page)
    {
        UserControl target = page switch
        {
            PageKind.Display => _displayPage!,
            PageKind.Presets => _presetsPage!,
            PageKind.Settings => _settingsPage!,
            PageKind.About => _aboutPage!,
            _ => _displayPage!
        };

        if (page == PageKind.Display)
            _vm.RefreshDisplays();

        PageHost.Content = target;
    }

    // =====================================================================
    // Toast notifications
    // =====================================================================

    private void OnNotified(NotificationKind kind, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ToastText.Text = message;
            ToastDot.Fill = kind switch
            {
                NotificationKind.Success => new SolidColorBrush(Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6)),
                NotificationKind.Error => new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0x71, 0x71)),
                _ => new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xA1, 0xAA))
            };

            AnimateToastOpacity(0, 1, 150);

            _toastTimer ??= CreateToastTimer();
            _toastTimer.Stop();
            _toastTimer.Start();
        });
    }

    private DispatcherQueueTimer CreateToastTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(2400);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            AnimateToastOpacity(1, 0, 200);
        };
        return timer;
    }

    private void AnimateToastOpacity(double from, double to, int ms)
    {
        var anim = new DoubleAnimation { From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(ms)) };
        Storyboard.SetTarget(anim, ToastBorder);
        Storyboard.SetTargetProperty(anim, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }

    // =====================================================================
    // Lifecycle: close-to-tray
    // =====================================================================

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_realExit)
        {
            HandleRealExit();
            return;
        }

        if (_vm.Storage.Current.MinimizeToTray)
        {
            // Intercept the close button → hide to tray instead of exiting.
            args.Handled = true;
            HideToTray(showNotification: true);
        }
        else
        {
            HandleRealExit();
        }
    }

    private MinimizeNotificationWindow? _minToast;

    private void HideToTray(bool showNotification)
    {
        AppWindow.Hide();
        EnsureTrayIcon();

        if (showNotification && _vm.Storage.Current.NotifyOnMinimize)
            ShowMinimizeToast();
    }

    /// <summary>Shows the own styled "minimized" popup (click = restore).</summary>
    private void ShowMinimizeToast()
    {
        try
        {
            CloseMinimizeToast();

            var toast = new MinimizeNotificationWindow();
            toast.RestoreRequested += OnTrayOpen;
            toast.Closed += (_, _) =>
            {
                if (ReferenceEquals(_minToast, toast))
                    _minToast = null;
            };
            _minToast = toast;
            toast.ShowPopup();
            Logger.Info("Minimized popup shown.");
        }
        catch (Exception ex)
        {
            Logger.Error("Minimize toast failed", ex);
        }
    }

    private void CloseMinimizeToast()
    {
        try { _minToast?.Close(); }
        catch { /* already gone */ }
        finally { _minToast = null; }
    }

    private void EnsureTrayIcon()
    {
        if (_tray.IsVisible)
            return;

        IntPtr hIcon = NativeMethods.ExtractIcon(IntPtr.Zero,
            Environment.ProcessPath ?? string.Empty, 0);

        _tray.OpenRequested += OnTrayOpen;
        _tray.RestartRequested += RestartApplication;
        _tray.ExitRequested += ExitApplication;
        _tray.Create(hIcon, "Reqwa Colors");
    }

    private void OnTrayOpen()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CloseMinimizeToast();
            AppWindow.Show();
            Activate();
        });
    }

    /// <summary>Launches a fresh instance of the app, then exits this one.</summary>
    public void RestartApplication()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("Restart failed to launch a new instance", ex);
        }

        ExitApplication();
    }

    /// <summary>Performs a real exit (called by Settings "Quit" action too).</summary>
    public void ExitApplication()
    {
        _realExit = true;
        _tray.Remove();
        Close();
    }

    private void HandleRealExit()
    {
        if (_vm.Storage.Current.RestoreOnExit)
            _vm.Color.ResetAllDisplays();

        _tray.Dispose();
    }
}
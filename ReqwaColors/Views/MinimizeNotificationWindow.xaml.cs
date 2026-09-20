using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ReqwaColors.Native;
using ReqwaColors.Services;
using Windows.Graphics;
using Windows.UI;

namespace ReqwaColors.Views;

/// <summary>
/// Own in-app "minimized" notification: a small borderless popup, styled like
/// the app (dark panel, purple accent), parked bottom-right. Shown WITHOUT
/// activating so it never steals focus (e.g. while gaming). Click restores
/// the main window; it dismisses itself after a few seconds.
/// </summary>
public sealed partial class MinimizeNotificationWindow : Window
{
    private static readonly SolidColorBrush NormalBg =
        new(Color.FromArgb(0xE6, 0x12, 0x15, 0x1D));
    private static readonly SolidColorBrush HoverBg =
        new(Color.FromArgb(0xE6, 0x1E, 0x1E, 0x28));

    private readonly DispatcherQueueTimer _timer;

    /// <summary>Raised when the user clicks the popup (restore the main window).</summary>
    public event Action? RestoreRequested;

    public MinimizeNotificationWindow()
    {
        InitializeComponent();

        Title = string.Empty;
        ExtendsContentIntoTitleBar = true;

        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        AppWindow.IsShownInSwitchers = false;

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(4.5);
        _timer.Tick += (_, _) => Close();
    }

    /// <summary>Positions bottom-right and shows without taking focus.</summary>
    public void ShowPopup()
    {
        try
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = NativeMethods.GetDpiForWindow(hwnd);
            if (dpi == 0) dpi = 96;
            double scale = dpi / 96.0;

            int w = (int)(360 * scale);
            int h = (int)(100 * scale);
            int margin = (int)(16 * scale);
            AppWindow.Resize(new SizeInt32(w, h));

            RectInt32 work = DisplayArea.Primary?.WorkArea
                ?? new RectInt32 { X = 0, Y = 0, Width = 1920, Height = 1040 };
            AppWindow.Move(new PointInt32(
                work.X + work.Width - w - margin,
                work.Y + work.Height - h - margin));

            // Show() without Activate(): visible, but never steals focus.
            AppWindow.Show();
            _timer.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("Minimize popup failed", ex);
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e) =>
        RestoreRequested?.Invoke();

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e) =>
        Root.Background = HoverBg;

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) =>
        Root.Background = NormalBg;
}

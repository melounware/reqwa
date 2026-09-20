using System.Runtime.InteropServices;
using ReqwaColors.Native;

namespace ReqwaColors.Services;

/// <summary>
/// Lightweight, event-driven system tray icon (Shell_NotifyIcon + a hidden
/// message window). Supports left-click restore, a right-click context menu
/// and balloon ("toast-style") notifications, all without third-party deps.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const uint IconId = 4213;

    // TrackPopupMenu return ids.
    private const int CmdOpen = 1;
    private const int CmdRestart = 2;
    private const int CmdExit = 3;

    private IntPtr _hwnd;
    private IntPtr _hicon;
    private bool _added;
    private string _tooltip = "Reqwa Colors";
    private WndProcDelegate? _proc;

    /// <summary>Left click / "Open" requested.</summary>
    public event Action? OpenRequested;

    /// <summary>Context menu "Restart" requested.</summary>
    public event Action? RestartRequested;

    /// <summary>Context menu "Exit" requested.</summary>
    public event Action? ExitRequested;

    /// <summary>Global Insert hotkey pressed (show / hide the window).</summary>
    public event Action? ToggleRequested;

    public bool IsVisible => _added;
    private bool _hotkeyRegistered;

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Creates the hidden message window (also hosts the hotkey).</summary>
    private bool EnsureWindow()
    {
        if (_hwnd != IntPtr.Zero)
            return true;

        _proc = WndProc;
        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            lpszClassName = "ReqwaColorsTrayWnd",
            hInstance = Marshal.GetHINSTANCE(typeof(TrayIconService).Module)
        };
        RegisterClassW(ref wc);

        _hwnd = CreateWindowExW(0, "ReqwaColorsTrayWnd", string.Empty, 0,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        return _hwnd != IntPtr.Zero;
    }

    /// <summary>Registers the global Insert hotkey. Works even before the icon is shown.</summary>
    public void RegisterToggleHotkey()
    {
        if (_hotkeyRegistered || !EnsureWindow())
            return;

        if (NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HOTKEY_ID_TOGGLE, 0, NativeMethods.VK_INSERT))
        {
            _hotkeyRegistered = true;
            Logger.Info("Insert hotkey registered.");
        }
        else
        {
            Logger.Warn("Insert hotkey registration failed (already taken?).");
        }
    }

    /// <summary>
    /// Creates the tray icon. <paramref name="iconHandle"/> is the HICON to show.
    /// </summary>
    public bool Create(IntPtr iconHandle, string tooltip)
    {
        if (_added)
            return true;

        _hicon = iconHandle;
        _tooltip = string.IsNullOrWhiteSpace(tooltip) ? "Reqwa Colors" : tooltip;

        if (!EnsureWindow())
            return false;

        var nid = BaseData();
        nid.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
        nid.uCallbackMessage = NativeMethods.WM_TRAYICON;
        nid.hIcon = _hicon;
        nid.szTip = _tooltip;

        _added = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref nid);
        if (!_added)
            Logger.Warn("Tray icon registration failed.");
        else
            Logger.Info("Tray icon created.");
        return _added;
    }

    /// <summary>A NOTIFYICONDATA primed with the shared handle/id and empty strings.</summary>
    private NativeMethods.NOTIFYICONDATA BaseData() => new()
    {
        cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = IconId,
        szTip = _tooltip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_hwnd == hwnd)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.HOTKEY_ID_TOGGLE)
            {
                ToggleRequested?.Invoke();
            }
            else if (msg == NativeMethods.WM_TRAYICON)
            {
                int m = lParam.ToInt32() & 0xFFFF;
                if (m == NativeMethods.WM_LBUTTONUP)
                    OpenRequested?.Invoke();
                else if (m == NativeMethods.WM_RBUTTONUP || m == NativeMethods.WM_CONTEXTMENU)
                    ShowContextMenu();
            }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        IntPtr menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)CmdOpen, "Open");
        NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
        NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)CmdRestart, "Restart");
        NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)CmdExit, "Exit");

        NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
        NativeMethods.SetForegroundWindow(_hwnd);

        int cmd = NativeMethods.TrackPopupMenu(
            menu,
            NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_BOTTOMALIGN |
            NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
            pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

        NativeMethods.DestroyMenu(menu);

        switch (cmd)
        {
            case CmdOpen:
                OpenRequested?.Invoke();
                break;
            case CmdRestart:
                RestartRequested?.Invoke();
                break;
            case CmdExit:
                ExitRequested?.Invoke();
                break;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    public void Remove()
    {
        if (!_added)
            return;

        var nid = BaseData();
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref nid);
        _added = false;
    }

    public void Dispose()
    {
        Remove();
        if (_hotkeyRegistered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HOTKEY_ID_TOGGLE);
            _hotkeyRegistered = false;
        }
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        if (_hicon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hicon);
            _hicon = IntPtr.Zero;
        }
    }
}

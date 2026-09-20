using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqwaColors.Models;

/// <summary>Which theme the application renders with. Dark is the Reqwa identity.</summary>
public enum AppTheme
{
    Dark
}

/// <summary>Which accent color is used. Purple is the Reqwa identity.</summary>
public enum AccentColor
{
    Purple
}

/// <summary>
/// Persistent application settings, stored as JSON in %LOCALAPPDATA%.
/// </summary>
public partial class AppSettings : ObservableObject
{
    /// <summary>JSON schema version, used by future migrations.</summary>
    public int SchemaVersion { get; set; } = 1;

    // ------------ General ------------
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _alwaysOnTop;

    // ------------ Appearance ------------
    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty]
    private AccentColor _accent = AccentColor.Purple;

    // ------------ Behavior ------------
    [ObservableProperty]
    private bool _applyLastColorsOnStartup;

    [ObservableProperty]
    private bool _restoreOnExit = true;

    [ObservableProperty]
    private bool _notifyOnMinimize = true;

    // ------------ Advanced ------------
    /// <summary>Display id ("\\.\DISPLAY1") → last applied settings.</summary>
    [ObservableProperty]
    private Dictionary<string, ColorSettings> _lastAppliedByDisplay = new();

    /// <summary>Id of the display selected on the Display page.</summary>
    [ObservableProperty]
    private string? _selectedDisplayId;
}
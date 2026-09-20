using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReqwaColors.Models;
using ReqwaColors.Services;

namespace ReqwaColors.ViewModels;

/// <summary>
/// Settings page state. Binds directly to the shared <see cref="AppSettings"/>
/// instance and persists on every change (cheap, atomic JSON write).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _storage;
    private readonly NotificationService _notifications;

    public AppSettings Settings { get; }

    public SettingsViewModel(SettingsService storage, NotificationService notifications)
    {
        _storage = storage;
        _notifications = notifications;
        Settings = storage.Current;

        Settings.PropertyChanged += OnSettingsChanged;

        // Sync the registry with the persisted preference on launch, so the
        // two can never drift (e.g. settings.json edited by hand).
        ApplyStartupRegistration(Settings.StartWithWindows);
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _storage.SaveCurrent();

        if (e.PropertyName == nameof(AppSettings.StartWithWindows))
            ApplyStartupRegistration(Settings.StartWithWindows);
    }

    public string DataDirectory => _storage.DataDirectory;
    public string LogDirectory => Logger.LogDirectory;
    public string Version => "1.0.0";

    /// <summary>Raised when the user chooses to fully quit (shell handles the exit).</summary>
    public event Action? ExitRequested;

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

    [RelayCommand]
    private void ResetSettings()
    {
        Settings.StartWithWindows = false;
        Settings.MinimizeToTray = true;
        Settings.StartMinimized = false;
        Settings.AlwaysOnTop = false;
        Settings.ApplyLastColorsOnStartup = false;
        Settings.RestoreOnExit = true;
        Settings.NotifyOnMinimize = true;
        Settings.LastAppliedByDisplay.Clear();
        _storage.SaveCurrent();
        _notifications.Info("Application settings reset.");
    }

    /// <summary>Registers/unregisters the app in the user's Startup folder (no admin needed).</summary>
    public void ApplyStartupRegistration(bool enabled)
    {
        try
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (key is null)
                return;

            if (enabled)
            {
                string exe = Environment.ProcessPath ?? string.Empty;
                if (exe.Length > 0)
                    key.SetValue("ReqwaColors", $"\"{exe}\" --minimized");
            }
            else
            {
                key.DeleteValue("ReqwaColors", throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Startup registration failed", ex);
        }
    }
}
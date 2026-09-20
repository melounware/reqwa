using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqwaColors.Models;
using ReqwaColors.Services;

namespace ReqwaColors.ViewModels;

/// <summary>
/// Backing state for the Display page: per-monitor slider values,
/// apply/reset commands and a live LUT for the before/after preview.
/// </summary>
public partial class DisplayViewModel : ObservableObject
{
    private readonly ColorService _color;
    private readonly DisplayService _displays;
    private readonly SettingsService _storage;
    private readonly NotificationService _notifications;

    /// <summary>Settings currently shown in the sliders (not necessarily applied).</summary>
    public ColorSettings Draft { get; }

    private DisplayInfo? _selectedDisplay;
    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (SetProperty(ref _selectedDisplay, value))
            {
                OnPropertyChanged(nameof(HasDisplay));
                OnPropertyChanged(nameof(HeaderSubtitle));

                if (value is not null)
                {
                    _storage.Current.SelectedDisplayId = value.DeviceName;
                    _storage.SaveCurrent();
                }
            }
        }
    }

    public bool HasDisplay => SelectedDisplay is not null;

    /// <summary>Updates command availability when the display list changes.</summary>
    public void HasDisplayIfAny(IReadOnlyList<DisplayInfo> displays)
    {
        ApplyCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasDisplays));
    }

    public bool HasDisplays => _displays.Displays.Count > 0;

    public string HeaderSubtitle => SelectedDisplay is null
        ? "Fine-tune your display colors."
        : $"Fine-tune {SelectedDisplay.Label} ({SelectedDisplay.ResolutionLabel}).";

    public DisplayViewModel(
        ColorService color,
        DisplayService displays,
        SettingsService storage,
        NotificationService notifications)
    {
        _color = color;
        _displays = displays;
        _storage = storage;
        _notifications = notifications;
        Draft = new ColorSettings();

        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => HasDisplay);
        ResetCommand = new RelayCommand(Reset, () => HasDisplay);

        // Re-evaluate the preview on any slider change (cheap: 3x256 LUT).
        Draft.PropertyChanged += (_, _) =>
        {
            PreviewLutVersion++;
            OnPropertyChanged(nameof(PreviewLutVersion));
        };
    }

    public IAsyncRelayCommand ApplyCommand { get; }
    public IRelayCommand ResetCommand { get; }

    /// <summary>Incremented whenever the preview LUT should be recomputed.</summary>
    public int PreviewLutVersion { get; private set; }

    private async Task ApplyAsync()
    {
        if (SelectedDisplay is null)
            return;

        ApplyCommand.NotifyCanExecuteChanged();

        OperationResult result = await Task.Run(() =>
            _color.Apply(SelectedDisplay.DeviceName, Draft));

        if (result.IsSuccess)
        {
            // Persist last-applied for startup restore.
            var appSettings = _storage.Current;
            appSettings.LastAppliedByDisplay[SelectedDisplay.PersistentId] = Draft.Clone();
            _storage.SaveCurrent();

            _notifications.Success(result.UserMessage);
        }
        else
        {
            _notifications.Error(result.UserMessage);
            Logger.Warn($"Apply failed on {SelectedDisplay.DeviceName}: {result.Detail}");
        }
    }

    private void Reset()
    {
        if (SelectedDisplay is null)
            return;

        OperationResult result = _color.Reset(SelectedDisplay.DeviceName);
        if (result.IsSuccess)
        {
            Draft.ResetToDefaults();
            _notifications.Info("Display reset to defaults.");
        }
        else
        {
            _notifications.Error(result.UserMessage);
        }
    }

    /// <summary>Reloads draft from persisted last-applied state (startup restore).</summary>
    public void LoadLastAppliedOrDefault()
    {
        var appSettings = _storage.Current;
        if (SelectedDisplay is not null &&
            appSettings.LastAppliedByDisplay.TryGetValue(SelectedDisplay.PersistentId, out ColorSettings? saved))
        {
            Draft.CopyFrom(saved);
        }
    }
}
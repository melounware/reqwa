using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using ReqwaColors.Models;
using ReqwaColors.Services;
using Windows.ApplicationModel.DataTransfer;

namespace ReqwaColors.ViewModels;

/// <summary>Wrapper that adds UI state (apply / rename / active) to a preset for card binding.</summary>
public partial class PresetItemViewModel : ObservableObject
{
    public PresetItemViewModel(Preset preset)
    {
        Preset = preset;
    }

    public Preset Preset { get; }

    public string Name => Preset.Name;
    public string Description => Preset.Description;
    public string Summary => Preset.Summary;
    public bool IsBuiltIn => Preset.IsBuiltIn;

    /// <summary>Refreshes the card after the underlying preset's name changes.</summary>
    public void RefreshName() => OnPropertyChanged(nameof(Name));

    [ObservableProperty]
    private bool _isApplying;

    /// <summary>True while this preset is the active one on the selected display.</summary>
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    /// <summary>UI visibility for the delete / rename buttons (built-ins can't be edited).</summary>
    public Visibility DeleteVisibility =>
        IsBuiltIn ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Name row (and action row) are shown when not renaming.</summary>
    public Visibility NameVisibility =>
        IsRenaming ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Rename editor is shown while renaming.</summary>
    public Visibility EditVisibility =>
        IsRenaming ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>"Applied" chip visibility.</summary>
    public Visibility ActiveVisibility =>
        IsActive ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsRenamingChanged(bool value)
    {
        OnPropertyChanged(nameof(NameVisibility));
        OnPropertyChanged(nameof(EditVisibility));
    }

    partial void OnIsActiveChanged(bool value) =>
        OnPropertyChanged(nameof(ActiveVisibility));

    // Commands routed into the owning page VM.
    public IRelayCommand DeleteCommand { get; set; } = null!;
    public IAsyncRelayCommand ApplyCommand { get; set; } = null!;
    public IRelayCommand ShareCommand { get; set; } = null!;
    public IRelayCommand BeginRenameCommand { get; set; } = null!;
    public IRelayCommand CommitRenameCommand { get; set; } = null!;
    public IRelayCommand CancelRenameCommand { get; set; } = null!;
}

/// <summary>
/// Presets page: shows built-in and user presets, applies them to the
/// selected monitor, and supports creating, renaming, deleting, sharing
/// (copy a code) and importing custom presets.
/// </summary>
public partial class PresetsViewModel : ObservableObject
{
    private readonly PresetsService _presets;
    private readonly ColorService _color;
    private readonly NotificationService _notifications;
    private readonly Func<DisplayInfo?> _selectedDisplayAccessor;
    private readonly Func<ColorSettings> _draftAccessor;

    public ObservableCollection<PresetItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    private string _importCode = string.Empty;

    public PresetsViewModel(
        PresetsService presets,
        ColorService color,
        NotificationService notifications,
        Func<DisplayInfo?> selectedDisplayAccessor,
        Func<ColorSettings> draftAccessor)
    {
        _presets = presets;
        _color = color;
        _notifications = notifications;
        _selectedDisplayAccessor = selectedDisplayAccessor;
        _draftAccessor = draftAccessor;

        foreach (Preset p in presets.UserPresets)
            Items.Add(CreateItem(p));

        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyVisibility));
    }

    /// <summary>Shows the "no presets yet" panel when the list is empty.</summary>
    public Visibility EmptyVisibility =>
        Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private PresetItemViewModel CreateItem(Preset p)
    {
        var item = new PresetItemViewModel(p);
        item.DeleteCommand = new RelayCommand(() => DeleteItem(item));
        item.ApplyCommand = new AsyncRelayCommand(() => ApplyCoreAsync(p, item));
        item.ShareCommand = new RelayCommand(() => Share(item));
        item.BeginRenameCommand = new RelayCommand(() => BeginRename(item), () => !item.IsBuiltIn);
        item.CommitRenameCommand = new RelayCommand(() => CommitRename(item));
        item.CancelRenameCommand = new RelayCommand(() => item.IsRenaming = false);
        return item;
    }

    partial void OnNewPresetNameChanged(string value) =>
        CreateFromCurrentCommand.NotifyCanExecuteChanged();

    partial void OnImportCodeChanged(string value) =>
        ImportFromCodeCommand.NotifyCanExecuteChanged();

    // =====================================================================
    // Apply
    // =====================================================================

    private async Task ApplyCoreAsync(Preset preset, PresetItemViewModel item)
    {
        DisplayInfo? display = _selectedDisplayAccessor();
        if (display is null)
        {
            _notifications.Info("Select a monitor first.");
            return;
        }

        item.IsApplying = true;
        try
        {
            OperationResult result = await Task.Run(() => _color.Apply(display.DeviceName, preset.Settings));
            if (result.IsSuccess)
            {
                foreach (PresetItemViewModel other in Items)
                    other.IsActive = ReferenceEquals(other, item);

                _draftAccessor().CopyFrom(preset.Settings);
                _notifications.Success($"“{preset.Name}” applied to {display.Label}.");
            }
            else
            {
                _notifications.Error(result.UserMessage);
            }
        }
        finally
        {
            item.IsApplying = false;
        }
    }

    // =====================================================================
    // Create / import
    // =====================================================================

    public IRelayCommand CreateFromCurrentCommand =>
        new RelayCommand(CreateFromCurrent, () => !string.IsNullOrWhiteSpace(NewPresetName));

    private void CreateFromCurrent()
    {
        Preset preset = _presets.CreateFromSettings(NewPresetName.Trim(), _draftAccessor());
        Items.Add(CreateItem(preset));
        NewPresetName = string.Empty;
        _notifications.Success($"Preset “{preset.Name}” saved.");
    }

    public IRelayCommand ImportFromCodeCommand =>
        new RelayCommand(ImportFromCode, () => !string.IsNullOrWhiteSpace(ImportCode));

    private void ImportFromCode()
    {
        Preset? preset = _presets.ImportCode(ImportCode);
        if (preset is null)
        {
            _notifications.Error("That shared code isn't valid.");
            return;
        }

        Items.Add(CreateItem(preset));
        ImportCode = string.Empty;
        _notifications.Success($"Imported “{preset.Name}”.");
    }

    // =====================================================================
    // Rename
    // =====================================================================

    private void BeginRename(PresetItemViewModel item)
    {
        item.RenameText = item.Name;
        item.IsRenaming = true;
    }

    private void CommitRename(PresetItemViewModel item)
    {
        string name = item.RenameText.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = item.Name;

        _presets.Rename(item.Preset, name);
        item.RefreshName();
        item.IsRenaming = false;
        _notifications.Success("Preset renamed.");
    }

    // =====================================================================
    // Share
    // =====================================================================

    private void Share(PresetItemViewModel item)
    {
        string code = _presets.ExportCode(item.Preset);
        if (TryCopyToClipboard(code))
            _notifications.Success($"“{item.Name}” copied — paste it to share.");
        else
            _notifications.Error("Couldn't copy to the clipboard.");
    }

    private static bool TryCopyToClipboard(string text)
    {
        try
        {
            DataPackage package = new();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Clipboard copy failed", ex);
            return false;
        }
    }

    // =====================================================================
    // Delete
    // =====================================================================

    private void DeleteItem(PresetItemViewModel item)
    {
        if (item.IsBuiltIn)
            return;
        _presets.Delete(item.Preset);
        Items.Remove(item);
    }
}

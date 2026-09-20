using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReqwaColors.Models;
using ReqwaColors.Services;

namespace ReqwaColors.ViewModels;

/// <summary>Top-level pages in the sidebar.</summary>
public enum PageKind
{
    Display,
    Presets,
    Settings,
    About
}

/// <summary>
/// Shell state: current page, service wiring shared by pages, and the
/// single source of truth for the selected monitor.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DisplayService _displays;
    private readonly NotificationService _notifications;

    public SettingsService Storage { get; }
    public PresetsService Presets { get; }
    public ColorService Color { get; }
    public DisplayService Displays { get; }
    public DisplayViewModel DisplayPage { get; }
    public PresetsViewModel PresetsPage { get; }
    public SettingsViewModel SettingsPage { get; }
    public NotificationService Notifications { get; }

    [ObservableProperty]
    private PageKind _currentPage = PageKind.Display;

    public MainViewModel()
    {
        var storage = new SettingsService();
        var displayService = new DisplayService();
        var presetsService = new PresetsService(storage);
        var colorService = new ColorService(displayService);
        var notificationsService = new NotificationService();

        Storage = storage;
        Displays = displayService;
        _displays = displayService;
        _notifications = notificationsService;
        Notifications = notificationsService;
        Presets = presetsService;
        Color = colorService;

        DisplayPage = new DisplayViewModel(colorService, displayService, storage, notificationsService);
        PresetsPage = new PresetsViewModel(
            presetsService, colorService, notificationsService,
            () => DisplayPage.SelectedDisplay,
            () => DisplayPage.Draft);
        SettingsPage = new SettingsViewModel(storage, notificationsService);
    }

    /// <summary>Re-applies the last persisted gamma ramp for every known display.</summary>
    public async Task ApplySavedColorsAsync()
    {
        var saved = Storage.Current.LastAppliedByDisplay;
        if (saved.Count == 0)
            return;

        var targets = Displays.Displays
            .Where(d => saved.TryGetValue(d.PersistentId, out _))
            .Select(d => (d.DeviceName, Settings: saved[d.PersistentId]))
            .ToList();

        if (targets.Count == 0)
            return;

        await Task.Run(() =>
        {
            foreach (var t in targets)
                Color.Apply(t.DeviceName, t.Settings);
        });
    }

    /// <summary>Enumerates monitors and restores the last selection / applied state.</summary>
    public void Initialize()
    {
        IReadOnlyList<DisplayInfo> displays = _displays.RefreshDisplays();
        DisplayPage.HasDisplayIfAny(displays);

        string? preferred = Storage.Current.SelectedDisplayId;
        DisplayInfo? target =
            displays.FirstOrDefault(d => d.DeviceName == preferred) ??
            displays.FirstOrDefault(d => d.IsPrimary) ??
            displays.FirstOrDefault();

        DisplayPage.SelectedDisplay = target;
        DisplayPage.LoadLastAppliedOrDefault();
    }

    /// <summary>Re-enumerates monitors (called when the Display page is opened or monitors change).</summary>
    public void RefreshDisplays()
    {
        IReadOnlyList<DisplayInfo> displays = _displays.RefreshDisplays();
        DisplayPage.HasDisplayIfAny(displays);

        if (DisplayPage.SelectedDisplay is null ||
            displays.All(d => d.DeviceName != DisplayPage.SelectedDisplay.DeviceName))
        {
            DisplayPage.SelectedDisplay =
                displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
            DisplayPage.LoadLastAppliedOrDefault();
        }
    }

    [RelayCommand]
    private void Navigate(string parameter)
    {
        if (Enum.TryParse(parameter, ignoreCase: true, out PageKind page))
            CurrentPage = page;
    }
}
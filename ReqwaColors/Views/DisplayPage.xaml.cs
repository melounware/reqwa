using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ReqwaColors.Models;
using ReqwaColors.Services;
using ReqwaColors.ViewModels;

namespace ReqwaColors.Views;

/// <summary>
/// Display page code-behind. Keeps thin: selection state lives in
/// <see cref="DisplayViewModel"/>, pure visuals here.
/// </summary>
public sealed partial class DisplayPage : UserControl
{
    private static readonly ColorRanges RangesInstance = new();

    public MainViewModel Vm { get; }

    public DisplayPage(MainViewModel vm)
    {
        Vm = vm;
        InitializeComponent();
        Ranges = RangesInstance;

        MonitorsList.ItemsSource = _monitors;
        RefreshMonitorList();
        Vm.DisplayPage.PropertyChanged += OnDisplayVmPropertyChanged;

        AdvancedPanel.Visibility = Visibility.Collapsed;

        Draft.PropertyChanged += (_, _) => { };
    }

    /// <summary>x:Bind source for range constants.</summary>
    public ColorRanges Ranges { get; }

    /// <summary>x:Bind helper: "6500 K" formatting for the temperature readout.</summary>
    public string FormatKelvin(double kelvin) => $"{kelvin:0} K";

    private ColorSettings Draft => Vm.DisplayPage.Draft;

    // =====================================================================
    // Monitor selector
    // =====================================================================

    private readonly ObservableCollection<MonitorCardItem> _monitors = new();

    /// <summary>
    /// Rebuilds the cards only when the monitor set actually changed; a
    /// rebuild recreates containers, so doing it on every click would clear
    /// the user's highlight.
    /// </summary>
    private void RefreshMonitorList()
    {
        if (_monitors.Select(m => m.Info.DeviceName)
                     .SequenceEqual(Vm.Displays.Displays.Select(d => d.DeviceName)))
        {
            SyncSelection();
            return;
        }

        _monitors.Clear();
        foreach (DisplayInfo d in Vm.Displays.Displays)
            _monitors.Add(new MonitorCardItem(d));

        SyncSelection();
    }

    private void OnMonitorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MonitorCardItem card })
            Vm.DisplayPage.SelectedDisplay = card.Info;

        SyncSelection();
    }

    /// <summary>Marks exactly the selected monitor's card; brushes rebind instantly.</summary>
    private void SyncSelection()
    {
        DisplayInfo? selected = Vm.DisplayPage.SelectedDisplay;
        foreach (MonitorCardItem card in _monitors)
            card.IsSelected = selected is not null && card.Info.DeviceName == selected.DeviceName;
    }

    private void OnMonitorPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MonitorCardItem card })
            card.IsHovered = true;
    }

    private void OnMonitorPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MonitorCardItem card })
            card.IsHovered = false;
    }

    private void OnDisplayVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayViewModel.HasDisplays))
            RefreshMonitorList();
        else if (e.PropertyName == nameof(DisplayViewModel.SelectedDisplay))
            SyncSelection();
    }

    private void OnAdvancedToggle(object sender, RoutedEventArgs e)
    {
        AdvancedPanel.Visibility = AdvancedToggle.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

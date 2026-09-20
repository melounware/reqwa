using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReqwaColors.ViewModels;

namespace ReqwaColors.Views;

/// <summary>Preset gallery page. All logic lives in <see cref="PresetsViewModel"/>.</summary>
public sealed partial class PresetsPage : UserControl
{
    public MainViewModel Vm { get; }

    public PresetsPage(MainViewModel vm)
    {
        Vm = vm;
        InitializeComponent();
    }

    // The flyout lives in a separate visual tree, so its items resolve the card's
    // view model through their inherited DataContext instead of x:Bind commands.
    private static PresetItemViewModel? ItemOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as PresetItemViewModel;

    private void OnShareClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
            item.ShareCommand.Execute(null);
    }

    private void OnRenameClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
            item.BeginRenameCommand.Execute(null);
    }

    private void OnDeleteClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
            item.DeleteCommand.Execute(null);
    }
}

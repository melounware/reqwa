using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using ReqwaColors.Models;
using Windows.UI;

namespace ReqwaColors.ViewModels;

/// <summary>
/// Card wrapper for one monitor. Selection/hover looks are plain bound
/// brushes so the highlight always follows state immediately — no visual
/// state manager involved.
/// </summary>
public partial class MonitorCardItem : ObservableObject
{
    public MonitorCardItem(DisplayInfo info) => Info = info;

    public DisplayInfo Info { get; }

    public string Label => Info.Label;
    public string ResolutionLabel => Info.ResolutionLabel;
    public string RoleLabel => Info.RoleLabel;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHovered;

    private static readonly SolidColorBrush BaseBorder = new(Color.FromArgb(0xFF, 0x24, 0x24, 0x2D));
    private static readonly SolidColorBrush SelectedBorder = new(Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6));
    private static readonly SolidColorBrush BaseBg = new(Color.FromArgb(0xFF, 0x10, 0x10, 0x16));
    private static readonly SolidColorBrush HoverBg = new(Color.FromArgb(0xFF, 0x15, 0x15, 0x1D));

    public Brush RingBrush => IsSelected ? SelectedBorder : BaseBorder;
    public Brush BgBrush => IsSelected || IsHovered ? HoverBg : BaseBg;

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(RingBrush));
    partial void OnIsHoveredChanged(bool value) => OnPropertyChanged(nameof(BgBrush));
}

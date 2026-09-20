using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqwaColors.Models;

/// <summary>
/// A named color configuration, shown on the Presets page and saved by users.
/// </summary>
public partial class Preset : ObservableObject
{
    /// <summary>Stable unique id.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    private string _name = "New preset";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private bool _isBuiltIn;
    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetProperty(ref _isBuiltIn, value);
    }

    public ColorSettings Settings { get; set; } = new();

    /// <summary>Values shown on the card, e.g. "B 72 · C 70 · G 1.05 · S 85".</summary>
    [JsonIgnore]
    public string Summary => Settings.ToSummary();

    /// <summary>Called after load / when values change so cards refresh.</summary>
    public void RefreshComputed()
    {
        OnPropertyChanged(nameof(Summary));
        Settings = Settings; // touch to raise change on Settings reference if replaced
    }
}
using ReqwaColors.Models;

namespace ReqwaColors.Services;

/// <summary>
/// High-level color orchestration on top of <see cref="DisplayService"/>:
/// apply/reset per display, remembers the last applied configuration so it
/// can be restored at next launch or on exit.
/// </summary>
public sealed class ColorService
{
    private readonly DisplayService _displays;

    public ColorService(DisplayService displays)
    {
        _displays = displays;
    }

    /// <summary>Applies settings to a display and records the last-applied state.</summary>
    public OperationResult Apply(string deviceName, ColorSettings settings)
    {
        OperationResult result = _displays.Apply(deviceName, settings);
        if (result.IsSuccess)
        {
            LastApplied[deviceName] = settings.Clone();
        }
        return result;
    }

    /// <summary>Applies the neutral ramp (identity LUT) to a display.</summary>
    public OperationResult Reset(string deviceName)
    {
        OperationResult result = _displays.Reset(deviceName);
        if (result.IsSuccess)
        {
            var neutral = new ColorSettings();
            LastApplied[deviceName] = neutral;
        }
        return result;
    }

    /// <summary>Display id → last settings successfully applied this session.</summary>
    public Dictionary<string, ColorSettings> LastApplied { get; } = new();

    /// <summary>Resets all displays to neutral (no state recording).</summary>
    public void ResetAllDisplays() => _displays.ResetAll();
}
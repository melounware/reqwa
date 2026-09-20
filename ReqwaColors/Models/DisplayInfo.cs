using System.ComponentModel;

namespace ReqwaColors.Models;

/// <summary>Metadata for one connected display output.</summary>
public sealed record DisplayInfo(
    string DeviceName,
    string DeviceString,
    int MonitorWidthPx,
    int MonitorHeightPx,
    int DesktopWidthPx,
    int DesktopHeightPx,
    int RefreshHz,
    bool IsPrimary,
    int ScreenIndex)
{
    /// <summary>Friendly label, e.g. "Monitor 1".</summary>
    public string Label => $"Monitor {ScreenIndex}";

    /// <summary>"1920 × 1080 · 60 Hz" style subtitle.</summary>
    public string ResolutionLabel =>
        $"{MonitorWidthPx} × {MonitorHeightPx} · {RefreshHz} Hz";

    /// <summary>Role badge: "Primary" or "Secondary".</summary>
    public string RoleLabel => IsPrimary ? "Primary" : "Secondary";

    /// <summary>Key used for storing per-display state (stable across sessions).</summary>
    public string PersistentId => DeviceName;

    /// <summary>Full tooltip text.</summary>
    public string Tooltip => $"{DeviceString}\n{DeviceName}";
}

/// <summary>Result of a single gamma-ramp apply/get operation.</summary>
public enum ApplyStatus
{
    /// <summary>Operation succeeded.</summary>
    Success,
    /// <summary>The display does not support gamma-ramp adjustment.</summary>
    NotSupported,
    /// <summary>The OS/driver rejected the ramp (e.g. protected content).</summary>
    Rejected,
    /// <summary>The display disappeared or is no longer enumerated.</summary>
    DisplayLost,
    /// <summary>Any other failure.</summary>
    Failed
}

/// <summary>Result wrapper for backend operations.</summary>
public readonly record struct OperationResult(ApplyStatus Status, string? Detail = null)
{
    public bool IsSuccess => Status == ApplyStatus.Success;

    public static OperationResult Ok() => new(ApplyStatus.Success);
    public static OperationResult Fail(ApplyStatus status, string detail) => new(status, detail);

    /// <summary>Friendly message suitable for the UI.</summary>
    public string UserMessage => Status switch
    {
        ApplyStatus.Success => "Changes applied",
        ApplyStatus.NotSupported => "This display doesn't support color adjustment.",
        ApplyStatus.Rejected => "Windows rejected these settings for this display.",
        ApplyStatus.DisplayLost => "This display is no longer available.",
        _ => "Unable to apply these settings to this display."
    };
}

/// <summary>Supported hardware capabilities for a display.</summary>
[Flags]
public enum DisplayCapabilities
{
    None = 0,
    /// <summary>Software gamma-ramp (brightness/contrast/gamma/RGB) works.</summary>
    GammaRamp = 1,
    /// <summary>DDC/CI brightness control (VCP 0x10) responded.</summary>
    DdcBrightness = 2,
    /// <summary>DDC/CI contrast control (VCP 0x12) responded.</summary>
    DdcContrast = 4
}
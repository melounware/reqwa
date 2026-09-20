using System.Runtime.InteropServices;
using ReqwaColors.Models;
using ReqwaColors.Native;

namespace ReqwaColors.Services;

/// <summary>
/// Enumerates displays and applies color adjustments through the GDI gamma
/// ramp (the documented mechanism used by f.lux, Twinkle Tray, etc.).
/// Each logical display device ("\\.\DISPLAY1") gets its own device context;
/// the DC handle is always released.
/// </summary>
public sealed class DisplayService : IDisposable
{
    // GDI ramps are constrained by the driver to roughly 0.5..3.0 around
    // identity; we stay conservative so rejection never happens in practice.
    private const ushort RampMax = 65535;

    private sealed class DisplayRuntime
    {
        public required DisplayInfo Info;
        public IntPtr Dc;
    }

    private readonly Dictionary<string, DisplayRuntime> _runtimes = new();
    private readonly object _gate = new();

    /// <summary>Currently enumerated displays, index-ordered.</summary>
    public IReadOnlyList<DisplayInfo> Displays { get; private set; } = Array.Empty<DisplayInfo>();

    /// <summary>Enumerates active display outputs. Safe to call repeatedly (e.g. after monitor changes).</summary>
    public IReadOnlyList<DisplayInfo> RefreshDisplays()
    {
        lock (_gate)
        {
            var list = new List<DisplayInfo>();
            var newRuntimes = new Dictionary<string, DisplayRuntime>();

            try
            {
                for (uint i = 0; ; i++)
                {
                    try
                    {
                        var dd = NativeMethods.DISPLAY_DEVICE.Create();
                        if (!NativeMethods.EnumDisplayDevices(null, i, ref dd, 0))
                            break;

                        if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ACTIVE) == 0)
                            continue; // inactive mirror driver etc.

                        var mode = NativeMethods.DEVMODE.Create();
                        if (!NativeMethods.EnumDisplaySettings(dd.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref mode))
                            continue;

                        bool isPrimary = (dd.StateFlags & NativeMethods.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                        int hz = mode.dmDisplayFrequency > 1 ? (int)mode.dmDisplayFrequency : 60;

                        var info = new DisplayInfo(
                            DeviceName: dd.DeviceName,
                            DeviceString: string.IsNullOrWhiteSpace(dd.DeviceString) ? "Display" : dd.DeviceString,
                            MonitorWidthPx: (int)mode.dmPelsWidth,
                            MonitorHeightPx: (int)mode.dmPelsHeight,
                            DesktopWidthPx: (int)mode.dmPelsWidth,
                            DesktopHeightPx: (int)mode.dmPelsHeight,
                            RefreshHz: hz,
                            IsPrimary: isPrimary,
                            ScreenIndex: list.Count + 1);

                        list.Add(info);

                        if (_runtimes.TryGetValue(info.DeviceName, out DisplayRuntime? existing))
                        {
                            existing.Info = info;
                            newRuntimes[info.DeviceName] = existing;
                        }
                        else
                        {
                            newRuntimes[info.DeviceName] = new DisplayRuntime { Info = info, Dc = IntPtr.Zero };
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed enumerating display index {i}", ex);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Display enumeration failed", ex);
            }

            // Release DCs of displays that disappeared.
            foreach (KeyValuePair<string, DisplayRuntime> kv in _runtimes)
            {
                if (!newRuntimes.ContainsKey(kv.Key))
                    ReleaseDc(kv.Value);
            }

            _runtimes.Clear();
            foreach (KeyValuePair<string, DisplayRuntime> kv in newRuntimes)
                _runtimes[kv.Key] = kv.Value;

            Displays = list;
            Logger.Info($"Enumerated {list.Count} display(s).");
            return list;
        }
    }

    /// <summary>
    /// Computes the 3x256 LUT for the given settings. Exposed for the preview.
    /// </summary>
    public static ushort[] ComputeChannelLut(
        double brightness, double contrast, double gamma,
        double channelScale, double temperatureScale)
    {
        var lut = new ushort[RawGammaRamp.Entries];
        double invGamma = 1.0 / Math.Max(0.05, gamma);

        for (int i = 0; i < RawGammaRamp.Entries; i++)
        {
            double x = i / (double)(RawGammaRamp.Entries - 1); // 0..1

            // Contrast around mid gray, then brightness scale, then gamma, then tint.
            double c = (x - 0.5) * (contrast / 100.0) + 0.5;
            double b = c * (brightness / 100.0);
            double g = Math.Pow(Math.Clamp(b, 0.0, 1.0), invGamma);
            double v = g * channelScale * temperatureScale;

            lut[i] = (ushort)Math.Clamp((int)Math.Round(v * RampMax), 0, RampMax);
        }

        return lut;
    }

    /// <summary>
    /// Applies <paramref name="settings"/> to the display with the given device name.
    /// </summary>
    public OperationResult Apply(string deviceName, ColorSettings settings)
    {
        lock (_gate)
        {
            if (!_runtimes.TryGetValue(deviceName, out DisplayRuntime? rt))
                return OperationResult.Fail(ApplyStatus.DisplayLost, deviceName);

            // Kelvin â†’ per-channel tint (normalized to neutral at 6500K).
            double tw = 1.0, tg = 1.0, tb = 1.0;
            if (settings.TemperatureEnabled)
                (tw, tg, tb) = KelvinToChannelScales(settings.Temperature);

            RawGammaRamp ramp = RawGammaRamp.Create();
            ramp.Red = ComputeChannelLut(settings.Brightness, settings.Contrast, settings.Gamma, settings.Red / 100.0, tw);
            ramp.Green = ComputeChannelLut(settings.Brightness, settings.Contrast, settings.Gamma, settings.Green / 100.0, tg);
            ramp.Blue = ComputeChannelLut(settings.Brightness, settings.Contrast, settings.Gamma, settings.Blue / 100.0, tb);

            IntPtr dc = rt.Dc;
            if (dc == IntPtr.Zero)
            {
                dc = NativeMethods.CreateDC(null, rt.Info.DeviceName, null, IntPtr.Zero);
                if (dc == IntPtr.Zero)
                    return OperationResult.Fail(ApplyStatus.NotSupported, $"CreateDC failed for {deviceName}");
                rt.Dc = dc;
            }

            bool ok = NativeMethods.SetDeviceGammaRamp(dc, ref ramp);
            if (ok)
            {
                Logger.Info($"Ramp applied to {deviceName}: {settings.ToSummary()}");
                return OperationResult.Ok();
            }

            // Some drivers only reject ramps that exceed their range â€” retry
            // with a slightly compressed ramp before giving up.
            CompressRamp(ref ramp, 0.92);
            ok = NativeMethods.SetDeviceGammaRamp(dc, ref ramp);
            if (ok)
            {
                Logger.Info($"Ramp applied to {deviceName} after compression.");
                return OperationResult.Ok();
            }

                        int win32Err = Marshal.GetLastWin32Error();
            Logger.Error($"SetDeviceGammaRamp failed for {deviceName} (win32={win32Err})");
            return OperationResult.Fail(ApplyStatus.Rejected, $"SetDeviceGammaRamp failed for {deviceName} (win32={win32Err})");
        }
    }

    /// <summary>Resets a display to the identity ramp.</summary>
    public OperationResult Reset(string deviceName)
    {
        lock (_gate)
        {
            if (!_runtimes.TryGetValue(deviceName, out DisplayRuntime? rt))
                return OperationResult.Fail(ApplyStatus.DisplayLost, deviceName);

            IntPtr dc = rt.Dc;
            if (dc == IntPtr.Zero)
            {
                dc = NativeMethods.CreateDC(null, rt.Info.DeviceName, null, IntPtr.Zero);
                if (dc == IntPtr.Zero)
                    return OperationResult.Fail(ApplyStatus.NotSupported, deviceName);
                rt.Dc = dc;
            }

            RawGammaRamp ramp = RawGammaRamp.Create();
            for (int i = 0; i < RawGammaRamp.Entries; i++)
            {
                ushort v = (ushort)(i * RampMax / (RawGammaRamp.Entries - 1));
                ramp.Red[i] = v;
                ramp.Green[i] = v;
                ramp.Blue[i] = v;
            }

            bool ok = NativeMethods.SetDeviceGammaRamp(dc, ref ramp);
            return ok
                ? OperationResult.Ok()
                : OperationResult.Fail(ApplyStatus.Rejected, $"Reset failed for {deviceName}");
        }
    }

    /// <summary>Resets every known display (used on exit when restore-on-exit is set).</summary>
    public void ResetAll()
    {
        lock (_gate)
        {
            foreach (string name in _runtimes.Keys.ToList())
                Reset(name);
        }
    }

    /// <summary>
    /// Kelvin â†’ RGB channel scales, normalized so 6500 K = (1,1,1).
    /// Uses Tanner Helland's approximation, simplified for our range.
    /// </summary>
    public static (double R, double G, double B) KelvinToChannelScales(double kelvin)
    {
        double t = Math.Clamp(kelvin, ColorRanges.TemperatureMin, ColorRanges.TemperatureMax) / 100.0;

        double r, g, b;
        if (t <= 66) r = 255;
        else r = 329.698727446 * Math.Pow(t - 60, -0.1332047592);
        if (t <= 66) g = 99.4708025861 * Math.Log(t) - 161.1195681661;
        else g = 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
        if (t >= 66) b = 255;
        else if (t <= 19) b = 0;
        else b = 138.5177312231 * Math.Log(t - 10) - 305.0447927307;

        r = Math.Clamp(r, 1, 255) / 255.0;
        g = Math.Clamp(g, 1, 255) / 255.0;
        b = Math.Clamp(b, 1, 255) / 255.0;

        // Normalize to 6500K â‰ˆ neutral so the slider doesn't darken globally.
        double r0 = 1.0, g0 = 1.0, b0 = 1.0;
        {
            double t0 = 65;
            double rr = 329.698727446 * Math.Pow(t0 - 60, -0.1332047592);
            double gg = 99.4708025861 * Math.Log(t0) - 161.1195681661;
            double bb = 138.5177312231 * Math.Log(t0 - 10) - 305.0447927307;
            r0 = Math.Clamp(rr, 1, 255) / 255.0;
            g0 = Math.Clamp(gg, 1, 255) / 255.0;
            b0 = Math.Clamp(bb, 1, 255) / 255.0;
        }

        return (r / r0, g / g0, b / b0);
    }

    private static void CompressRamp(ref RawGammaRamp ramp, double factor)
    {
        for (int i = 0; i < RawGammaRamp.Entries; i++)
        {
            ramp.Red[i] = (ushort)Math.Clamp((int)(ramp.Red[i] * factor), 0, RampMax);
            ramp.Green[i] = (ushort)Math.Clamp((int)(ramp.Green[i] * factor), 0, RampMax);
            ramp.Blue[i] = (ushort)Math.Clamp((int)(ramp.Blue[i] * factor), 0, RampMax);
        }
    }

    private void ReleaseDc(DisplayRuntime rt)
    {
        if (rt.Dc != IntPtr.Zero)
        {
            NativeMethods.DeleteDC(rt.Dc);
            rt.Dc = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (DisplayRuntime rt in _runtimes.Values)
                ReleaseDc(rt);
            _runtimes.Clear();
        }
    }
}

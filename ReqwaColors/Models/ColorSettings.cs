using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqwaColors.Models;

/// <summary>Range metadata for a color control slider. (Instantiable for x:Bind.)</summary>
public sealed class ColorRanges
{
    public const double BrightnessMin = 50;
    public const double BrightnessMax = 150;
    public const double ContrastMin = 50;
    public const double ContrastMax = 150;
    public const double GammaMin = 0.5;
    public const double GammaMax = 2.5;

    public const double TemperatureMin = 2500;
    public const double TemperatureMax = 9500;
    public const double TemperatureDefault = 6500;

    public const double DefaultsBrightness = 100;
    public const double DefaultsContrast = 100;
    public const double DefaultsGamma = 1.0;

    /// <summary>Clamps <paramref name="value"/> into [min, max].</summary>
    public static double Clamp(double value, double min, double max)
        => double.IsNaN(value) ? min : Math.Clamp(value, min, max);
}

/// <summary>
/// A complete set of display color adjustments. Values are stored in
/// "user units" (100 = neutral) so they map 1:1 onto UI sliders and JSON.
/// Gamma is the exception: 1.0 = neutral, > 1 brightens midtones.
/// </summary>
public partial class ColorSettings : ObservableObject
{
    private double _brightness = ColorRanges.DefaultsBrightness;
    private double _contrast = ColorRanges.DefaultsContrast;
    private double _gamma = ColorRanges.DefaultsGamma;
    private double _red = 100;
    private double _green = 100;
    private double _blue = 100;
    private double _temperature = ColorRanges.TemperatureDefault;
    private bool _temperatureEnabled;

    /// <summary>Brightness multiplier in percent (100 = neutral).</summary>
    public double Brightness
    {
        get => _brightness;
        set => SetProperty(ref _brightness, ColorRanges.Clamp(value, ColorRanges.BrightnessMin, ColorRanges.BrightnessMax));
    }

    /// <summary>Contrast multiplier in percent (100 = neutral).</summary>
    public double Contrast
    {
        get => _contrast;
        set => SetProperty(ref _contrast, ColorRanges.Clamp(value, ColorRanges.ContrastMin, ColorRanges.ContrastMax));
    }

    /// <summary>Gamma exponent (1.0 = neutral, > 1 brightens midtones).</summary>
    public double Gamma
    {
        get => _gamma;
        set => SetProperty(ref _gamma, ColorRanges.Clamp(value, ColorRanges.GammaMin, ColorRanges.GammaMax));
    }

    /// <summary>Red channel multiplier in percent (100 = neutral).</summary>
    public double Red
    {
        get => _red;
        set => SetProperty(ref _red, ColorRanges.Clamp(value, 0, 200));
    }

    /// <summary>Green channel multiplier in percent (100 = neutral).</summary>
    public double Green
    {
        get => _green;
        set => SetProperty(ref _green, ColorRanges.Clamp(value, 0, 200));
    }

    /// <summary>Blue channel multiplier in percent (100 = neutral).</summary>
    public double Blue
    {
        get => _blue;
        set => SetProperty(ref _blue, ColorRanges.Clamp(value, 0, 200));
    }

    /// <summary>Target color temperature in Kelvin. Applied as warm/cool channel tinting.</summary>
    public double Temperature
    {
        get => _temperature;
        set => SetProperty(ref _temperature, ColorRanges.Clamp(value, ColorRanges.TemperatureMin, ColorRanges.TemperatureMax));
    }

    /// <summary>Whether the temperature tint participates in the computed ramp.</summary>
    public bool TemperatureEnabled
    {
        get => _temperatureEnabled;
        set => SetProperty(ref _temperatureEnabled, value);
    }

    /// <summary>True when any channel differs from its neutral value.</summary>
    public bool IsModified =>
        Brightness != ColorRanges.DefaultsBrightness ||
        Contrast != ColorRanges.DefaultsContrast ||
        Gamma != ColorRanges.DefaultsGamma ||
        Red != 100 || Green != 100 || Blue != 100 ||
        (TemperatureEnabled && Temperature != ColorRanges.TemperatureDefault);

    /// <summary>Resets every channel to neutral.</summary>
    public void ResetToDefaults()
    {
        Brightness = ColorRanges.DefaultsBrightness;
        Contrast = ColorRanges.DefaultsContrast;
        Gamma = ColorRanges.DefaultsGamma;
        Red = Green = Blue = 100;
        Temperature = ColorRanges.TemperatureDefault;
        TemperatureEnabled = false;
    }

    /// <summary>Copies all values from <paramref name="other"/>.</summary>
    public void CopyFrom(ColorSettings other)
    {
        Brightness = other.Brightness;
        Contrast = other.Contrast;
        Gamma = other.Gamma;
        Red = other.Red;
        Green = other.Green;
        Blue = other.Blue;
        Temperature = other.Temperature;
        TemperatureEnabled = other.TemperatureEnabled;
    }

    /// <summary>Creates a deep copy of this instance.</summary>
    public ColorSettings Clone()
    {
        var c = new ColorSettings();
        c.CopyFrom(this);
        return c;
    }

    /// <summary>True when all values are equal within a small epsilon.</summary>
    public bool EqualsSettings(ColorSettings other) =>
        Math.Abs(Brightness - other.Brightness) < 0.01 &&
        Math.Abs(Contrast - other.Contrast) < 0.01 &&
        Math.Abs(Gamma - other.Gamma) < 0.001 &&
        Math.Abs(Red - other.Red) < 0.01 &&
        Math.Abs(Green - other.Green) < 0.01 &&
        Math.Abs(Blue - other.Blue) < 0.01 &&
        Math.Abs(Temperature - other.Temperature) < 1 &&
        TemperatureEnabled == other.TemperatureEnabled;

    /// <summary>Short single-line summary, e.g. "B 72 · C 65 · G 1.05".</summary>
    public string ToSummary()
    {
        string fmt(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        return $"B {fmt(Brightness)} · C {fmt(Contrast)} · G {Gamma.ToString("0.00", CultureInfo.InvariantCulture)}";
    }
}
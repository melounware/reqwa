using System.Text;
using ReqwaColors.Models;

namespace ReqwaColors.Services;

/// <summary>
/// Owns the preset catalog (user-created presets only).
/// Persists presets via <see cref="SettingsService"/>.
/// </summary>
public sealed class PresetsService
{
    private readonly SettingsService _storage;

    public PresetsService(SettingsService storage)
    {
        _storage = storage;
        UserPresets = storage.LoadUserPresets();
    }

    /// <summary>Presets created by the user.</summary>
    public List<Preset> UserPresets { get; }

    /// <summary>Creates a preset from current slider values and persists it.</summary>
    public Preset CreateFromSettings(string name, ColorSettings settings)
    {
        var preset = new Preset { Name = name, Description = "Custom preset", IsBuiltIn = false };
        preset.Settings.CopyFrom(settings);
        UserPresets.Add(preset);
        Save();
        return preset;
    }

    public void Delete(Preset preset)
    {
        UserPresets.Remove(preset);
        Save();
    }

    /// <summary>Renames a user preset and persists the catalog.</summary>
    public void Rename(Preset preset, string name)
    {
        preset.Name = name;
        if (!preset.IsBuiltIn)
            Save();
    }

    public void Save()
    {
        _storage.SaveUserPresets(UserPresets);
    }

    // ======================= Share (export / import) =======================
    //
    // Compact binary layout (base64url encoded):
    //   [0]='R' [1]='C' [2]=version(2) [3]=flags(bit0: temperature on)
    //   [4]=brightness-50  [5]=contrast-50  [6]=gamma*100-50  [7]=reserved(was saturation, ignored)
    //   [8]=red [9]=green [10]=blue [11]=(temperature-2500)/100
    //   [12]=name length in bytes, then UTF-8 name.

    private const byte CodeVersion = 2;

    /// <summary>Encodes a preset into a compact, shareable one-line code.</summary>
    public string ExportCode(Preset preset)
    {
        ColorSettings s = preset.Settings;
        byte[] nameBytes = Encoding.UTF8.GetBytes(preset.Name.Trim());
        if (nameBytes.Length > 255)
            Array.Resize(ref nameBytes, 255);

        byte[] data = new byte[13 + nameBytes.Length];
        data[0] = (byte)'R';
        data[1] = (byte)'C';
        data[2] = CodeVersion;
        data[3] = (byte)(s.TemperatureEnabled ? 1 : 0);
        data[4] = (byte)Math.Clamp((int)Math.Round(s.Brightness) - 50, 0, 255);
        data[5] = (byte)Math.Clamp((int)Math.Round(s.Contrast) - 50, 0, 255);
        data[6] = (byte)Math.Clamp((int)Math.Round(s.Gamma * 100) - 50, 0, 255);
        data[7] = 100; // reserved slot (was saturation); kept so old codes still import
        data[8] = (byte)Math.Clamp((int)Math.Round(s.Red), 0, 255);
        data[9] = (byte)Math.Clamp((int)Math.Round(s.Green), 0, 255);
        data[10] = (byte)Math.Clamp((int)Math.Round(s.Blue), 0, 255);
        data[11] = (byte)Math.Clamp((int)Math.Round((s.Temperature - 2500) / 100), 0, 255);
        data[12] = (byte)nameBytes.Length;
        Buffer.BlockCopy(nameBytes, 0, data, 13, nameBytes.Length);

        return ToBase64Url(data);
    }

    /// <summary>Decodes a shared code into a new user preset. Returns null if invalid.</summary>
    public Preset? ImportCode(string code)
    {
        try
        {
            byte[] data = FromBase64Url(code.Trim());
            if (data.Length < 13 || data[0] != (byte)'R' || data[1] != (byte)'C' || data[2] != CodeVersion)
                return null;

            var settings = new ColorSettings
            {
                TemperatureEnabled = (data[3] & 1) != 0,
                Brightness = data[4] + 50,
                Contrast = data[5] + 50,
                Gamma = (data[6] + 50) / 100.0,
                // data[7] is reserved (was saturation, which had no display effect).
                Red = data[8],
                Green = data[9],
                Blue = data[10],
                Temperature = data[11] * 100 + 2500,
            };

            string name = "Shared preset";
            int nameLen = data[12];
            if (nameLen > 0 && data.Length >= 13 + nameLen)
            {
                string parsed = Encoding.UTF8.GetString(data, 13, nameLen).Trim();
                if (parsed.Length > 0)
                    name = parsed;
            }

            var preset = new Preset
            {
                Name = name,
                Description = "Imported preset",
                IsBuiltIn = false
            };
            preset.Settings.CopyFrom(settings);

            UserPresets.Add(preset);
            Save();
            return preset;
        }
        catch (Exception ex)
        {
            Logger.Error("Preset import failed", ex);
            return null;
        }
    }

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string code)
    {
        string padded = code.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
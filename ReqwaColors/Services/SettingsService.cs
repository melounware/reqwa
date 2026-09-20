using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReqwaColors.Models;

namespace ReqwaColors.Services;

/// <summary>
/// Loads and saves application state as JSON under %LOCALAPPDATA%\ReqwaColors.
/// Writes are atomic (temp file + File.Move) so a crash can never corrupt
/// user data. Invalid files are quarantined instead of crashing.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DataDirectory { get; }

    private AppSettings? _current;

    /// <summary>Shared settings instance, loaded once and kept in memory.</summary>
    public AppSettings Current => _current ??= LoadSettings();

    private string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    private string PresetsPath => Path.Combine(DataDirectory, "presets.json");

    public SettingsService()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReqwaColors");
        try
        {
            Directory.CreateDirectory(DataDirectory);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not create data directory", ex);
        }
    }

    // ======================= Settings =======================

    public AppSettings LoadSettings()
    {
        if (!TryRead(SettingsPath, out string json))
            return new AppSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            Logger.Info("Settings loaded.");
            return settings ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            Logger.Error($"Corrupt settings.json quarantined: {ex.Message}");
            Quarantine(SettingsPath);
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        _current = settings;
        WriteAtomically(SettingsPath, settings);
    }

    /// <summary>Saves the shared <see cref="Current"/> instance.</summary>
    public void SaveCurrent() => SaveSettings(Current);

    // ======================= Presets =======================

    /// <summary>Loads user presets. Built-ins are re-created by PresetsService.</summary>
    public List<Preset> LoadUserPresets()
    {
        if (!TryRead(PresetsPath, out string json))
            return new List<Preset>();

        try
        {
            var presets = JsonSerializer.Deserialize<List<Preset>>(json, JsonOptions) ?? new();
            foreach (Preset p in presets)
                p.RefreshComputed();
            return presets;
        }
        catch (JsonException ex)
        {
            Logger.Error($"Corrupt presets.json quarantined: {ex.Message}");
            Quarantine(PresetsPath);
            return new List<Preset>();
        }
    }

    public void SaveUserPresets(IReadOnlyList<Preset> userPresets)
    {
        WriteAtomically(PresetsPath, userPresets);
    }

    // ======================= Helpers =======================

    private bool TryRead(string path, out string json)
    {
        json = string.Empty;
        try
        {
            if (!File.Exists(path))
                return false;
            json = File.ReadAllText(path);
            return json.Trim().Length > 0;
        }
        catch (IOException ex)
        {
            Logger.Error($"Could not read {path}", ex);
            return false;
        }
    }

    private void WriteAtomically<T>(string path, T value)
    {
        try
        {
            string dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error($"Could not save {path}", ex);
        }
    }

    private void Quarantine(string path)
    {
        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch
        {
            // best effort
        }
    }
}
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DCSLogViewer.Models;

/// <summary>
/// Persisted application configuration (paths, tools, preferences).
/// Saved as JSON next to the .exe.
/// </summary>
public class AppConfig
{
    public string DcsInstallPath { get; set; } = "";
    public string DcsSavedGamesPath { get; set; } = "";
    public string DcsVariant { get; set; } = "DCS"; // "DCS" or "DCS.openbeta"
    public List<ExternalTool> ExternalTools { get; set; } = new();
    public string LastActiveTab { get; set; } = "Launcher";
    public bool MinimizeToTray { get; set; }
    public bool StartWithWindows { get; set; }

    // Derived paths
    [JsonIgnore] public string ConfigFolder => Path.Combine(DcsSavedGamesPath, DcsVariant, "Config");
    [JsonIgnore] public string OptionsLuaPath => Path.Combine(ConfigFolder, "options.lua");
    [JsonIgnore] public string AutoexecCfgPath => Path.Combine(ConfigFolder, "autoexec.cfg");
    [JsonIgnore] public string LogsFolder => Path.Combine(DcsSavedGamesPath, DcsVariant, "Logs");
    [JsonIgnore] public string DcsExePath => Path.Combine(DcsInstallPath, "bin", "DCS.exe");

    private static readonly string ConfigFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "dcs_manager_config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Attempts to auto-detect DCS installation and saved games paths.
    /// </summary>
    public void AutoDetect()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DcsSavedGamesPath = Path.Combine(userProfile, "Saved Games");

        // Detect variant
        if (Directory.Exists(Path.Combine(DcsSavedGamesPath, "DCS")))
            DcsVariant = "DCS";
        else if (Directory.Exists(Path.Combine(DcsSavedGamesPath, "DCS.openbeta")))
            DcsVariant = "DCS.openbeta";

        // Try common install locations
        string[] installCandidates =
        [
            @"C:\Program Files\Eagle Dynamics\DCS World",
            @"C:\Program Files\Eagle Dynamics\DCS World OpenBeta",
            @"C:\Program Files (x86)\Steam\steamapps\common\DCSWorld",
            @"D:\DCS World",
            @"D:\Games\DCS World",
            @"E:\DCS World",
        ];

        foreach (var path in installCandidates)
        {
            if (File.Exists(Path.Combine(path, "bin", "DCS.exe")))
            {
                DcsInstallPath = path;
                break;
            }
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null) return config;
            }
        }
        catch { /* Fall through to defaults */ }

        var newConfig = new AppConfig();
        newConfig.AutoDetect();
        return newConfig;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch { /* Silently fail on save errors */ }
    }
}

using System.IO;

namespace DCSLogViewer.Models;

/// <summary>
/// Predefined profiles for common DCS log file locations.
/// </summary>
public class DcsLogProfile
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// Resolves the full file path using the current user's profile.
    /// </summary>
    public string GetFullPath()
    {
        var savedGames = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games");
        return Path.Combine(savedGames, RelativePath);
    }

    public bool FileExists() => File.Exists(GetFullPath());

    /// <summary>
    /// All known DCS log file locations.
    /// </summary>
    public static readonly DcsLogProfile[] KnownProfiles =
    [
        new()
        {
            Name = "DCS Stable - Main Log",
            Description = "Primary log for DCS World stable release",
            RelativePath = @"DCS\Logs\dcs.log"
        },
        new()
        {
            Name = "DCS OpenBeta - Main Log",
            Description = "Primary log for DCS World open beta",
            RelativePath = @"DCS.openbeta\Logs\dcs.log"
        },
        new()
        {
            Name = "DCS Stable - Previous Log",
            Description = "Previous session log (stable)",
            RelativePath = @"DCS\Logs\dcs.log.old"
        },
        new()
        {
            Name = "DCS OpenBeta - Previous Log",
            Description = "Previous session log (open beta)",
            RelativePath = @"DCS.openbeta\Logs\dcs.log.old"
        },
        new()
        {
            Name = "DCS Stable - Lua Export Log",
            Description = "Lua scripting export log (stable)",
            RelativePath = @"DCS\Logs\lua.log"
        },
        new()
        {
            Name = "DCS OpenBeta - Lua Export Log",
            Description = "Lua scripting export log (open beta)",
            RelativePath = @"DCS.openbeta\Logs\lua.log"
        }
    ];
}

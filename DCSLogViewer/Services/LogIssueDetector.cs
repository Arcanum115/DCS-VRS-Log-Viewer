using System.Text.RegularExpressions;
using DCSLogViewer.Models;

namespace DCSLogViewer.Services;

/// <summary>
/// Scans DCS log entries for known issue patterns and returns
/// user-friendly descriptions of detected issues.
/// Each pattern has a unique IssueKey to avoid duplicate detections.
/// </summary>
public class LogIssueDetector
{
    private readonly List<IssuePattern> _patterns;

    public LogIssueDetector()
    {
        _patterns = BuildPatterns();
    }

    /// <summary>
    /// Check a single log entry against all known patterns.
    /// Returns any matching issues (usually 0 or 1).
    /// </summary>
    public IEnumerable<DetectedIssue> Check(LogEntry entry)
    {
        var text = entry.RawLine ?? "";
        if (string.IsNullOrWhiteSpace(text)) yield break;

        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(text))
            {
                yield return new DetectedIssue
                {
                    Title = pattern.Title,
                    Description = pattern.Description,
                    Severity = pattern.Severity,
                    Category = pattern.Category,
                    MatchedLogLine = text.Length > 300 ? text[..300] + "..." : text,
                    LineNumber = entry.LineNumber,
                    IssueKey = pattern.IssueKey
                };
            }
        }
    }

    private static List<IssuePattern> BuildPatterns()
    {
        return new List<IssuePattern>
        {
            // ===== CRASHES & FATAL =====
            new IssuePattern
            {
                IssueKey = "crash_access_violation",
                Title = "Access Violation (Crash)",
                Description = "DCS crashed with an access violation. This is typically caused by corrupted game files, bad mods, or driver issues.",
                Severity = IssueSeverity.Critical,
                Category = "Crash",
                MatchText = "ACCESS_VIOLATION"
            },
            new IssuePattern
            {
                IssueKey = "crash_out_of_memory",
                Title = "Out of Memory",
                Description = "DCS ran out of available memory (RAM or VRAM). DCS is very memory hungry, especially on large multiplayer maps.",
                Severity = IssueSeverity.Critical,
                Category = "Memory",
                MatchText = "out of memory",
                CaseInsensitive = true
            },
            new IssuePattern
            {
                IssueKey = "crash_page_file",
                Title = "Page File / Virtual Memory Exhausted",
                Description = "Windows ran out of virtual memory. DCS commonly needs 20+ GB of committed memory on complex missions.",
                Severity = IssueSeverity.Critical,
                Category = "Memory",
                MatchText = "page file",
                CaseInsensitive = true
            },
            new IssuePattern
            {
                IssueKey = "crash_dxgi_removed",
                Title = "GPU Device Removed (DXGI_ERROR_DEVICE_REMOVED)",
                Description = "The GPU driver crashed or was reset. This often happens with unstable overclocks, overheating, or driver bugs.",
                Severity = IssueSeverity.Critical,
                Category = "GPU",
                MatchText = "DXGI_ERROR_DEVICE_REMOVED"
            },
            new IssuePattern
            {
                IssueKey = "crash_d3d_device_lost",
                Title = "Direct3D Device Lost",
                Description = "The graphics device was lost unexpectedly. Similar to GPU device removed but can also be caused by driver timeout (TDR).",
                Severity = IssueSeverity.Critical,
                Category = "GPU",
                MatchText = "D3D device lost",
                CaseInsensitive = true
            },

            // ===== GRAPHICS =====
            new IssuePattern
            {
                IssueKey = "gfx_shader_error",
                Title = "Shader Compilation Error",
                Description = "A shader failed to compile. This can cause missing visual effects, black textures, or graphical glitches.",
                Severity = IssueSeverity.Error,
                Category = "Graphics",
                Pattern = new Regex(@"(?:shader|SHADER).*(?:error|failed|compilation)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "gfx_texture_not_found",
                Title = "Missing Texture",
                Description = "DCS could not find a texture file. This usually means a mod is broken or game files are incomplete.",
                Severity = IssueSeverity.Warning,
                Category = "Graphics",
                Pattern = new Regex(@"(?:texture|TEX).*(?:not found|missing|failed to load)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "gfx_render_target",
                Title = "Render Target Creation Failed",
                Description = "DCS failed to create a render target, likely due to insufficient VRAM or resolution set too high.",
                Severity = IssueSeverity.Error,
                Category = "Graphics",
                Pattern = new Regex(@"render.?target.*(?:fail|error|cannot)", RegexOptions.IgnoreCase)
            },

            // ===== MODS =====
            new IssuePattern
            {
                IssueKey = "mod_script_error",
                Title = "Lua Script Error (Mod)",
                Description = "A Lua script error occurred. This is almost always caused by a mod, mission script, or custom livery.",
                Severity = IssueSeverity.Warning,
                Category = "Mods",
                Pattern = new Regex(@"SCRIPTING.*(?:ERROR|error|Runtime)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "mod_integrity_check",
                Title = "Integrity Check Failure",
                Description = "DCS detected modified or corrupted game files. Some multiplayer servers require file integrity and will kick you.",
                Severity = IssueSeverity.Warning,
                Category = "Mods",
                Pattern = new Regex(@"integrity.*(?:check|fail|mismatch)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "mod_not_installed",
                Title = "Required Module Not Installed",
                Description = "A mission or server requires a DCS module (aircraft/map) that you don't own or haven't installed.",
                Severity = IssueSeverity.Warning,
                Category = "Mods",
                Pattern = new Regex(@"module.*(?:not installed|not found|missing)", RegexOptions.IgnoreCase)
            },

            // ===== SOUND =====
            new IssuePattern
            {
                IssueKey = "sound_device_error",
                Title = "Sound Device Error",
                Description = "DCS could not initialize or use the audio device. You may have no sound in game.",
                Severity = IssueSeverity.Error,
                Category = "Sound",
                Pattern = new Regex(@"(?:SOUND|sound|audio).*(?:error|fail|not found|cannot|device)", RegexOptions.IgnoreCase)
            },

            // ===== NETWORK / MULTIPLAYER =====
            new IssuePattern
            {
                IssueKey = "net_timeout",
                Title = "Network Timeout",
                Description = "Connection to a server or DCS services timed out. Could be your internet, the server, or DCS auth servers.",
                Severity = IssueSeverity.Warning,
                Category = "Network",
                Pattern = new Regex(@"(?:timeout|timed out|connection.*(?:refused|reset|failed))", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "net_auth_failed",
                Title = "Authentication Failed",
                Description = "DCS could not verify your login. This prevents access to multiplayer and licensed modules.",
                Severity = IssueSeverity.Error,
                Category = "Network",
                Pattern = new Regex(@"(?:auth|login|license).*(?:fail|error|denied|invalid)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "net_desync",
                Title = "Multiplayer Desync",
                Description = "You've desynced from the server. Other players may see you in the wrong position or you may see glitched behavior.",
                Severity = IssueSeverity.Warning,
                Category = "Network",
                Pattern = new Regex(@"(?:desync|out of sync|synchronization.*error)", RegexOptions.IgnoreCase)
            },

            // ===== TERRAIN / MAPS =====
            new IssuePattern
            {
                IssueKey = "terrain_load_error",
                Title = "Terrain Loading Error",
                Description = "DCS failed to load terrain data. This can cause missing ground textures, invisible terrain, or crashes when loading a map.",
                Severity = IssueSeverity.Error,
                Category = "Terrain",
                Pattern = new Regex(@"(?:terrain|TERRAIN).*(?:error|fail|cannot|not found|corrupt)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "terrain_tile_missing",
                Title = "Missing Terrain Tile",
                Description = "A terrain tile could not be loaded. You may see holes or flat areas in the landscape.",
                Severity = IssueSeverity.Warning,
                Category = "Terrain",
                Pattern = new Regex(@"(?:tile|TILE).*(?:missing|not found|error|fail)", RegexOptions.IgnoreCase)
            },

            // ===== INPUT / CONTROLS =====
            new IssuePattern
            {
                IssueKey = "input_device_error",
                Title = "Input Device Error",
                Description = "A joystick, throttle, or other input device could not be initialized. Your bindings may not work.",
                Severity = IssueSeverity.Warning,
                Category = "Input",
                Pattern = new Regex(@"(?:input|joystick|controller).*(?:error|fail|not found|cannot)", RegexOptions.IgnoreCase)
            },

            // ===== DISK / FILE SYSTEM =====
            new IssuePattern
            {
                IssueKey = "disk_write_error",
                Title = "Disk Write Error",
                Description = "DCS could not write to disk. This can prevent saving settings, track files, or screenshots.",
                Severity = IssueSeverity.Error,
                Category = "Disk",
                Pattern = new Regex(@"(?:write|save|create).*(?:error|fail|denied|permission)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "disk_read_error",
                Title = "File Read Error",
                Description = "DCS could not read a required file. The file may be missing, locked by another program, or corrupted.",
                Severity = IssueSeverity.Error,
                Category = "Disk",
                Pattern = new Regex(@"(?:can't open|cannot open|failed to open|failed to read|file.*not found)", RegexOptions.IgnoreCase)
            },

            // ===== DCS-SPECIFIC KNOWN ISSUES =====
            new IssuePattern
            {
                IssueKey = "dcs_metashader_rebuild",
                Title = "Metashader Rebuild In Progress",
                Description = "DCS is rebuilding its shader cache. This happens after updates or driver changes and causes long load times and stuttering on first run.",
                Severity = IssueSeverity.Info,
                Category = "Graphics",
                Pattern = new Regex(@"(?:metashader|fxo).*(?:build|compile|rebuild|generating)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "dcs_track_replay_error",
                Title = "Track Replay Desync",
                Description = "A track replay has desynced. DCS track replays are notoriously unreliable, especially on complex missions.",
                Severity = IssueSeverity.Info,
                Category = "Replay",
                Pattern = new Regex(@"(?:track|replay).*(?:desync|error|mismatch)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "dcs_lua_config_error",
                Title = "Configuration Lua Error",
                Description = "DCS found an error in a configuration .lua file. This can reset your settings to defaults.",
                Severity = IssueSeverity.Error,
                Category = "Config",
                Pattern = new Regex(@"(?:options|config).*\.lua.*(?:error|syntax|unexpected|malformed)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "dcs_vr_init_fail",
                Title = "VR Initialization Failed",
                Description = "DCS could not start in VR mode. Your headset may not be detected or VR runtime is not running.",
                Severity = IssueSeverity.Error,
                Category = "VR",
                Pattern = new Regex(@"(?:VR|OpenVR|Oculus|SteamVR).*(?:fail|error|not found|cannot|init)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "dcs_multiplayer_kick",
                Title = "Kicked from Server",
                Description = "You were disconnected or kicked from a multiplayer server.",
                Severity = IssueSeverity.Warning,
                Category = "Network",
                Pattern = new Regex(@"(?:kicked|banned|disconnected by server|removed from)", RegexOptions.IgnoreCase)
            },
            new IssuePattern
            {
                IssueKey = "dcs_livery_error",
                Title = "Livery/Skin Error",
                Description = "A custom livery or skin failed to load. The aircraft will use the default skin instead.",
                Severity = IssueSeverity.Warning,
                Category = "Mods",
                Pattern = new Regex(@"(?:livery|liveries|skin).*(?:error|fail|not found|missing)", RegexOptions.IgnoreCase)
            },
        };
    }

    /// <summary>
    /// Internal pattern definition used to match log lines.
    /// </summary>
    private class IssuePattern
    {
        public string IssueKey { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public IssueSeverity Severity { get; init; }
        public string Category { get; init; } = "";

        // Match by simple text contains
        public string? MatchText { get; init; }
        public bool CaseInsensitive { get; init; }

        // Match by regex
        public Regex? Pattern { get; init; }

        public bool IsMatch(string line)
        {
            if (MatchText != null)
            {
                var comparison = CaseInsensitive
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return line.Contains(MatchText, comparison);
            }

            if (Pattern != null)
                return Pattern.IsMatch(line);

            return false;
        }
    }
}

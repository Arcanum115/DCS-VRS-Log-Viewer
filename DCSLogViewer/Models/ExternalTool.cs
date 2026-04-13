using System.IO;

namespace DCSLogViewer.Models;

/// <summary>
/// Represents an external tool that can be launched from the app (OvGME, DLSS Swapper, etc.).
/// </summary>
public class ExternalTool
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "New Tool";
    public string ExePath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string IconHint { get; set; } = "wrench"; // For UI icon selection
    public bool RunAsAdmin { get; set; }

    /// <summary>URL to the tool's homepage/download page.</summary>
    public string ProjectUrl { get; set; } = "";

    public bool IsValid => !string.IsNullOrWhiteSpace(ExePath) && File.Exists(ExePath);

    /// <summary>
    /// Common DCS community tools with download/project URLs.
    /// </summary>
    public static ExternalTool[] SuggestedTools =>
    [
        new() { Name = "OvGME", IconHint = "package",
                ProjectUrl = "https://wiki.hoggitworld.com/view/OVGME" },
new() { Name = "DLSS Swapper", IconHint = "gpu",
                ProjectUrl = "https://github.com/beeradmoore/dlss-swapper" },
        new() { Name = "DCS Updater", IconHint = "download",
                ProjectUrl = "https://forum.dcs.world/topic/134493-the-dcs-updater-launcher-gui-utility-version-20-2023/" },
        new() { Name = "SRS (SimpleRadio)", IconHint = "radio",
                ProjectUrl = "http://dcssimpleradio.com/" },
        new() { Name = "Tacview", IconHint = "map",
                ProjectUrl = "https://www.tacview.net/download/" },
        new() { Name = "DCS The Way", IconHint = "navigation",
                ProjectUrl = "https://github.com/jonsky752/DCSTheWay" },
    ];
}

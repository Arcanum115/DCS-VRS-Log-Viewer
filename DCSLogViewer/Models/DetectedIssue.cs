namespace DCSLogViewer.Models;

/// <summary>
/// Severity level for a detected issue.
/// </summary>
public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// A known issue detected by scanning the DCS log.
/// </summary>
public record DetectedIssue
{
    /// <summary>Short title of the problem.</summary>
    public string Title { get; init; } = "";

    /// <summary>Detailed explanation of what went wrong.</summary>
    public string Description { get; init; } = "";

    /// <summary>How bad is it?</summary>
    public IssueSeverity Severity { get; init; }

    /// <summary>Category for grouping (e.g. "Graphics", "Mods", "Sound").</summary>
    public string Category { get; init; } = "";

    /// <summary>The log line(s) that triggered this detection.</summary>
    public string MatchedLogLine { get; init; } = "";

    /// <summary>Line number in the log file.</summary>
    public int LineNumber { get; init; }

    /// <summary>Unique key to prevent duplicate detections of the same issue type.</summary>
    public string IssueKey { get; init; } = "";
}

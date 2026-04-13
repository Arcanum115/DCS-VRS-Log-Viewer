namespace DCSLogViewer.Models;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Fatal,
    Unknown
}

public record LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string RawLine { get; init; } = string.Empty;
    public int LineNumber { get; init; }

    /// <summary>
    /// Parses a raw DCS log line into a structured LogEntry.
    /// DCS log lines typically look like:
    ///   2024-01-15 10:30:45.123 INFO    EDCORE: Some message here
    ///   00042.567 WARNING SOUND: Audio device not found
    ///   === Log opened UTC 2024-01-15 10:30:00
    /// </summary>
    public static LogEntry Parse(string rawLine, int lineNumber)
    {
        var entry = new LogEntry
        {
            RawLine = rawLine,
            LineNumber = lineNumber
        };

        if (string.IsNullOrWhiteSpace(rawLine))
            return entry with { Level = LogLevel.Trace, Message = rawLine };

        // Try parsing full timestamp format: "2024-01-15 10:30:45.123 LEVEL ..."
        if (TryParseFullTimestamp(rawLine, lineNumber, out var parsed))
            return parsed;

        // Try parsing elapsed time format: "00042.567 LEVEL ..."
        if (TryParseElapsedTime(rawLine, lineNumber, out parsed))
            return parsed;

        // Fallback: detect level from content
        var level = DetectLevelFromContent(rawLine);
        return entry with { Level = level, Message = rawLine };
    }

    private static bool TryParseFullTimestamp(string line, int lineNumber, out LogEntry result)
    {
        result = null!;

        // Match: "YYYY-MM-DD HH:MM:SS.fff" (23 chars minimum)
        if (line.Length < 24 || line[4] != '-' || line[7] != '-' || line[13] != ':')
            return false;

        var timestampStr = line[..23];
        if (!DateTime.TryParse(timestampStr, out var timestamp))
            return false;

        var remainder = line[23..].TrimStart();
        var (level, category, message) = ParseLevelCategoryMessage(remainder);

        result = new LogEntry
        {
            Timestamp = timestamp,
            Level = level,
            Category = category,
            Message = message,
            RawLine = line,
            LineNumber = lineNumber
        };
        return true;
    }

    private static bool TryParseElapsedTime(string line, int lineNumber, out LogEntry result)
    {
        result = null!;

        // Match: "00042.567 LEVEL ..." - elapsed seconds with decimal
        int dotIndex = -1;
        int spaceIndex = -1;

        for (int i = 0; i < Math.Min(line.Length, 15); i++)
        {
            if (line[i] == '.' && dotIndex == -1) dotIndex = i;
            else if (line[i] == ' ' && dotIndex > 0) { spaceIndex = i; break; }
            else if (!char.IsDigit(line[i]) && line[i] != '.') return false;
        }

        if (dotIndex <= 0 || spaceIndex <= 0)
            return false;

        if (!double.TryParse(line[..spaceIndex], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            return false;

        var remainder = line[spaceIndex..].TrimStart();
        var (level, category, message) = ParseLevelCategoryMessage(remainder);

        result = new LogEntry
        {
            Timestamp = DateTime.MinValue, // elapsed time, no absolute timestamp
            Level = level,
            Category = category,
            Message = message,
            RawLine = line,
            LineNumber = lineNumber
        };
        return true;
    }

    private static (LogLevel level, string category, string message) ParseLevelCategoryMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (LogLevel.Unknown, "", text);

        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var levelStr = parts[0].TrimEnd(':').ToUpperInvariant();
        var level = ParseLevel(levelStr);

        if (level == LogLevel.Unknown)
            return (DetectLevelFromContent(text), "", text);

        var afterLevel = parts.Length > 1 ? parts[1] : "";

        // Try to extract category (e.g., "EDCORE: message" or "GRAPHICSVISTA: message")
        var colonIdx = afterLevel.IndexOf(':');
        if (colonIdx > 0 && colonIdx < 40 && !afterLevel[..colonIdx].Contains(' '))
        {
            var category = afterLevel[..colonIdx].Trim();
            var message = afterLevel[(colonIdx + 1)..].TrimStart();
            return (level, category, message);
        }

        return (level, "", afterLevel);
    }

    private static LogLevel ParseLevel(string s) => s switch
    {
        "TRACE" => LogLevel.Trace,
        "DEBUG" => LogLevel.Debug,
        "INFO" => LogLevel.Info,
        "LOG" => LogLevel.Info,
        "WARNING" => LogLevel.Warning,
        "WARN" => LogLevel.Warning,
        "ERROR" => LogLevel.Error,
        "ERR" => LogLevel.Error,
        "FATAL" => LogLevel.Fatal,
        "CRITICAL" => LogLevel.Fatal,
        _ => LogLevel.Unknown
    };

    private static LogLevel DetectLevelFromContent(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("ERROR") || upper.Contains("EXCEPTION") || upper.Contains("FAILED"))
            return LogLevel.Error;
        if (upper.Contains("WARNING") || upper.Contains("WARN"))
            return LogLevel.Warning;
        if (upper.Contains("===") || upper.Contains("---"))
            return LogLevel.Info;
        return LogLevel.Unknown;
    }
}

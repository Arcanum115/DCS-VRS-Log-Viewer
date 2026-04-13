using System.IO;
using System.Text;
using DCSLogViewer.Models;

namespace DCSLogViewer.Services;

/// <summary>
/// Watches a log file for changes and emits parsed LogEntry objects in real-time.
/// Uses a polling approach with FileSystemWatcher notifications for reliability.
/// </summary>
public sealed class LogFileWatcher : IDisposable
{
    private readonly string _filePath;
    private readonly CancellationTokenSource _cts = new();
    private FileSystemWatcher? _fsWatcher;
    private long _lastPosition;
    private int _lineNumber;
    private Task? _pollingTask;

    public event Action<LogEntry>? EntryReceived;
    public event Action<string>? Error;
    public event Action? FileReset;

    public string FilePath => _filePath;
    public bool IsWatching => _pollingTask != null && !_pollingTask.IsCompleted;

    public LogFileWatcher(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// Start watching the file. Reads all existing content first, then tails new lines.
    /// </summary>
    public void Start(bool readExisting = true)
    {
        if (IsWatching) return;

        var directory = Path.GetDirectoryName(_filePath);
        var fileName = Path.GetFileName(_filePath);

        if (directory == null)
            throw new InvalidOperationException($"Invalid file path: {_filePath}");

        // Set up FileSystemWatcher for change notifications
        _fsWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _lastPosition = 0;
        _lineNumber = 0;

        if (!readExisting && File.Exists(_filePath))
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _lastPosition = fs.Length;
        }

        // Start the polling loop
        _pollingTask = Task.Run(() => PollLoop(_cts.Token), _cts.Token);
    }

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                ReadNewLines();
                await Task.Delay(250, ct); // Poll every 250ms
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Error?.Invoke($"Error reading {_filePath}: {ex.Message}");
                try { await Task.Delay(2000, ct); } catch { break; }
            }
        }
    }

    private void ReadNewLines()
    {
        using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Detect file truncation/rotation (DCS recreates log on launch)
        if (fs.Length < _lastPosition)
        {
            _lastPosition = 0;
            _lineNumber = 0;
            FileReset?.Invoke();
        }

        if (fs.Length == _lastPosition)
            return;

        fs.Seek(_lastPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 8192, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            _lineNumber++;
            var entry = LogEntry.Parse(line, _lineNumber);
            EntryReceived?.Invoke(entry);
        }

        _lastPosition = fs.Position;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _fsWatcher?.Dispose();
        _cts.Dispose();
    }
}

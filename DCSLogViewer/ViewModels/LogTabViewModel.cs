using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DCSLogViewer.Models;
using DCSLogViewer.Services;

namespace DCSLogViewer.ViewModels;

/// <summary>
/// ViewModel for a single log file tab. Manages the watcher, filtering, and display.
/// </summary>
public partial class LogTabViewModel : ObservableObject, IDisposable
{
    private readonly LogFileWatcher _watcher;
    private readonly LogIssueDetector _issueDetector = new();
    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private readonly HashSet<string> _seenIssueKeys = new();

    [ObservableProperty] private string _tabTitle = "Log";
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _showTrace = true;
    [ObservableProperty] private bool _showDebug = true;
    [ObservableProperty] private bool _showInfo = true;
    [ObservableProperty] private bool _showWarning = true;
    [ObservableProperty] private bool _showError = true;
    [ObservableProperty] private bool _showFatal = true;
    [ObservableProperty] private bool _showUnknown = true;
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private bool _isWatching;
    [ObservableProperty] private int _totalLines;
    [ObservableProperty] private int _filteredLines;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _issueCount;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _showIssuesPanel;

    public ObservableCollection<LogEntry> AllEntries { get; } = new();
    public ObservableCollection<DetectedIssue> DetectedIssues { get; } = new();
    public ICollectionView FilteredEntries { get; }

    /// <summary>
    /// Raised when a new error/warning is detected (for notification support).
    /// </summary>
    public event Action<LogEntry>? AlertEntry;

    /// <summary>
    /// Raised when auto-scroll should trigger.
    /// </summary>
    public event Action? ScrollToEnd;

    public LogTabViewModel(string filePath, Dispatcher dispatcher)
    {
        _filePath = filePath;
        _dispatcher = dispatcher;
        _tabTitle = Path.GetFileName(filePath);
        _watcher = new LogFileWatcher(filePath);

        // Set up collection view with filtering
        BindingOperations.EnableCollectionSynchronization(AllEntries, _lock);
        FilteredEntries = CollectionViewSource.GetDefaultView(AllEntries);
        FilteredEntries.Filter = FilterPredicate;

        // Wire up watcher events
        _watcher.EntryReceived += OnEntryReceived;
        _watcher.FileReset += OnFileReset;
        _watcher.Error += OnWatcherError;
    }

    partial void OnSearchTextChanged(string value) => RefreshFilter();
    partial void OnShowTraceChanged(bool value) => RefreshFilter();
    partial void OnShowDebugChanged(bool value) => RefreshFilter();
    partial void OnShowInfoChanged(bool value) => RefreshFilter();
    partial void OnShowWarningChanged(bool value) => RefreshFilter();
    partial void OnShowErrorChanged(bool value) => RefreshFilter();
    partial void OnShowFatalChanged(bool value) => RefreshFilter();
    partial void OnShowUnknownChanged(bool value) => RefreshFilter();

    [RelayCommand]
    private void StartWatching()
    {
        if (IsWatching) return;
        try
        {
            _watcher.Start(readExisting: true);
            IsWatching = true;
            StatusMessage = $"Watching: {FilePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopWatching()
    {
        _watcher.Dispose();
        IsWatching = false;
        StatusMessage = "Stopped";
    }

    [RelayCommand]
    private void ClearLog()
    {
        lock (_lock)
        {
            AllEntries.Clear();
        }
        TotalLines = 0;
        FilteredLines = 0;
        ErrorCount = 0;
        WarningCount = 0;
        IssueCount = 0;
        DetectedIssues.Clear();
        _seenIssueKeys.Clear();
        StatusMessage = "Log cleared";
    }

    [RelayCommand]
    private void ShowErrorsOnly()
    {
        ShowTrace = false;
        ShowDebug = false;
        ShowInfo = false;
        ShowWarning = true;
        ShowError = true;
        ShowFatal = true;
        ShowUnknown = false;
    }

    [RelayCommand]
    private void ShowAll()
    {
        ShowTrace = true;
        ShowDebug = true;
        ShowInfo = true;
        ShowWarning = true;
        ShowError = true;
        ShowFatal = true;
        ShowUnknown = true;
    }

    [RelayCommand]
    private void ToggleIssuesPanel()
    {
        ShowIssuesPanel = !ShowIssuesPanel;
    }

    [RelayCommand]
    private void ScanAllForIssues()
    {
        _seenIssueKeys.Clear();
        DetectedIssues.Clear();

        foreach (var entry in AllEntries)
        {
            foreach (var issue in _issueDetector.Check(entry))
            {
                if (_seenIssueKeys.Add(issue.IssueKey))
                    DetectedIssues.Add(issue);
            }
        }

        IssueCount = DetectedIssues.Count;
        ShowIssuesPanel = true;
        StatusMessage = $"Scan complete: {IssueCount} issue(s) detected";
    }

    private void OnEntryReceived(LogEntry entry)
    {
        _dispatcher.BeginInvoke(() =>
        {
            lock (_lock)
            {
                AllEntries.Add(entry);
            }

            TotalLines++;

            if (entry.Level == LogLevel.Error || entry.Level == LogLevel.Fatal)
            {
                ErrorCount++;
                AlertEntry?.Invoke(entry);
            }
            else if (entry.Level == LogLevel.Warning)
            {
                WarningCount++;
            }

            // Auto-detect issues in real time
            foreach (var issue in _issueDetector.Check(entry))
            {
                if (_seenIssueKeys.Add(issue.IssueKey))
                {
                    DetectedIssues.Add(issue);
                    IssueCount = DetectedIssues.Count;
                }
            }

            UpdateFilteredCount();

            if (AutoScroll)
                ScrollToEnd?.Invoke();
        });
    }

    private void OnFileReset()
    {
        _dispatcher.BeginInvoke(() =>
        {
            ClearLog();
            StatusMessage = "File was recreated - DCS restarted?";
        });
    }

    private void OnWatcherError(string error)
    {
        _dispatcher.BeginInvoke(() => StatusMessage = error);
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not LogEntry entry) return false;

        // Level filter
        var levelMatch = entry.Level switch
        {
            LogLevel.Trace => ShowTrace,
            LogLevel.Debug => ShowDebug,
            LogLevel.Info => ShowInfo,
            LogLevel.Warning => ShowWarning,
            LogLevel.Error => ShowError,
            LogLevel.Fatal => ShowFatal,
            LogLevel.Unknown => ShowUnknown,
            _ => true
        };

        if (!levelMatch) return false;

        // Text search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return entry.RawLine.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private void RefreshFilter()
    {
        FilteredEntries.Refresh();
        UpdateFilteredCount();
    }

    private void UpdateFilteredCount()
    {
        // Count filtered items
        int count = 0;
        foreach (var item in FilteredEntries)
            count++;
        FilteredLines = count;
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}

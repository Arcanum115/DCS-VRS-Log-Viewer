using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DCSLogViewer.Models;
using Microsoft.Win32;

namespace DCSLogViewer.ViewModels;

/// <summary>
/// Main window ViewModel. Coordinates navigation, sub-ViewModels, and global state.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher;

    [ObservableProperty] private string _globalStatus = "VRS // DCS Log Viewer ready";
    [ObservableProperty] private string _activePanel = "Launcher";

    // Sub-ViewModels
    public LauncherViewModel Launcher { get; }
    public PerformanceViewModel Performance { get; }
    public AppConfig Config { get; }

    // Log viewer state (kept from original)
    [ObservableProperty] private LogTabViewModel? _selectedTab;
    public ObservableCollection<LogTabViewModel> Tabs { get; } = new();
    public ObservableCollection<DcsLogProfile> AvailableProfiles { get; } = new();

    public MainViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        // Load or create config
        Config = AppConfig.Load();

        // Initialize sub-ViewModels
        Launcher = new LauncherViewModel(Config);
        Performance = new PerformanceViewModel(Config);

        // Refresh disk info when user changes DCS path
        Launcher.DcsPathChanged += () => Performance.GatherDiskInfo();

        RefreshProfiles();
    }

    // === NAVIGATION ===

    [RelayCommand]
    private void NavigateTo(string? panel)
    {
        if (string.IsNullOrWhiteSpace(panel)) return;
        ActivePanel = panel;
        GlobalStatus = panel switch
        {
            "Launcher" => "Launch DCS and your tools",
            "LogViewer" => "Real-time log monitoring",
            "Performance" => "System information and DCS details",
            "FpsGuide" => "DCS optimization with DCS-Max",
            "WinTweaks" => "Windows optimization with WinUtil by Chris Titus Tech",
            _ => ""
        };
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* Silently fail if browser can't open */ }
    }

    // === LOG VIEWER (preserved from original) ===

    [RelayCommand]
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open DCS Log File",
            Filter = "Log files (*.log)|*.log|Old log files (*.old)|*.old|All files (*.*)|*.*",
            InitialDirectory = GetDefaultDcsLogsFolder(),
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
                OpenLogFile(file);
        }
    }

    [RelayCommand]
    private void OpenProfile(DcsLogProfile? profile)
    {
        if (profile == null) return;
        var path = profile.GetFullPath();
        if (File.Exists(path))
            OpenLogFile(path);
        else
            GlobalStatus = $"File not found: {path}";
    }

    [RelayCommand]
    private void CloseTab(LogTabViewModel? tab)
    {
        if (tab == null) return;

        try
        {
            var title = tab.TabTitle;
            tab.StopWatchingCommand.Execute(null);

            // Clear selection before removing to avoid binding errors
            if (SelectedTab == tab)
                SelectedTab = Tabs.Count > 1 ? Tabs.FirstOrDefault(t => t != tab) : null;

            Tabs.Remove(tab);
            tab.Dispose();

            if (Tabs.Count > 0 && SelectedTab == null)
                SelectedTab = Tabs[^1];

            GlobalStatus = $"Closed: {title}";
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Error closing tab: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RefreshProfiles()
    {
        AvailableProfiles.Clear();
        foreach (var profile in DcsLogProfile.KnownProfiles)
        {
            if (profile.FileExists())
                AvailableProfiles.Add(profile);
        }
    }

    [RelayCommand]
    private void AutoDetectAndOpen()
    {
        RefreshProfiles();
        if (AvailableProfiles.Count == 0)
        {
            GlobalStatus = "No DCS log files found. Is DCS installed?";
            return;
        }

        var mainLog = AvailableProfiles.FirstOrDefault(p => p.Name.Contains("Main Log"));
        OpenLogFile(mainLog != null ? mainLog.GetFullPath() : AvailableProfiles[0].GetFullPath());
    }

    private void OpenLogFile(string filePath)
    {
        if (Tabs.Any(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedTab = Tabs.First(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            GlobalStatus = $"Already open: {Path.GetFileName(filePath)}";
            return;
        }

        var tab = new LogTabViewModel(filePath, _dispatcher);
        tab.AlertEntry += entry =>
        {
            var msg = $"[{entry.Level}] {entry.Category}: {entry.Message}";
            GlobalStatus = msg[..Math.Min(150, msg.Length)];
        };
        Tabs.Add(tab);
        SelectedTab = tab;
        tab.StartWatchingCommand.Execute(null);
        GlobalStatus = $"Opened: {filePath}";

        // Auto-switch to log viewer
        ActivePanel = "LogViewer";
    }

    private static string GetDefaultDcsLogsFolder()
    {
        var savedGames = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games");

        var stable = Path.Combine(savedGames, "DCS", "Logs");
        if (Directory.Exists(stable)) return stable;

        var beta = Path.Combine(savedGames, "DCS.openbeta", "Logs");
        if (Directory.Exists(beta)) return beta;

        return savedGames;
    }

    // === CONFIG ===

    [RelayCommand]
    private void SaveConfig()
    {
        Config.Save();
        GlobalStatus = "Configuration saved.";
    }
}

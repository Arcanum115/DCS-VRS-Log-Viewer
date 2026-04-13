using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DCSLogViewer.Models;
using Microsoft.Win32;

namespace DCSLogViewer.ViewModels;

/// <summary>
/// ViewModel for the Launcher panel - launch DCS and external tools.
/// </summary>
public partial class LauncherViewModel : ObservableObject
{
    private readonly AppConfig _config;

    [ObservableProperty] private string _dcsInstallPath = "";
    [ObservableProperty] private string _dcsStatus = "Not detected";
    [ObservableProperty] private bool _isDcsRunning;
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<ExternalTool> Tools { get; } = new();

    public LauncherViewModel(AppConfig config)
    {
        _config = config;
        DcsInstallPath = config.DcsInstallPath;
        UpdateDcsStatus();

        foreach (var tool in config.ExternalTools)
            Tools.Add(tool);
    }

    [RelayCommand]
    private void LaunchDcs()
    {
        var exePath = _config.DcsExePath;
        if (!File.Exists(exePath))
        {
            StatusMessage = $"DCS.exe not found at: {exePath}";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
                UseShellExecute = true
            });
            StatusMessage = "DCS World launched!";
            UpdateDcsStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to launch DCS: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenProjectPage(ExternalTool? tool)
    {
        if (tool == null || string.IsNullOrWhiteSpace(tool.ProjectUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = tool.ProjectUrl, UseShellExecute = true });
            StatusMessage = $"Opened {tool.Name} project page in browser.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open URL: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LaunchTool(ExternalTool? tool)
    {
        if (tool == null) return;
        if (!tool.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(tool.ProjectUrl))
                StatusMessage = $"{tool.Name} not found. Click the link icon to download it from its project page.";
            else
                StatusMessage = $"{tool.Name}: exe not found. Use Browse to set its path.";
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tool.ExePath,
                Arguments = tool.Arguments,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(tool.WorkingDirectory))
                psi.WorkingDirectory = tool.WorkingDirectory;
            else
                psi.WorkingDirectory = Path.GetDirectoryName(tool.ExePath) ?? "";

            if (tool.RunAsAdmin)
                psi.Verb = "runas";

            Process.Start(psi);
            StatusMessage = $"{tool.Name} launched!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to launch {tool.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddTool()
    {
        var tool = new ExternalTool { Name = "New Tool" };
        Tools.Add(tool);
        SaveTools();
        StatusMessage = "New tool added - set its path to configure.";
    }

    [RelayCommand]
    private void BrowseToolPath(ExternalTool? tool)
    {
        if (tool == null) return;

        var dialog = new OpenFileDialog
        {
            Title = $"Select executable for {tool.Name}",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            tool.ExePath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(tool.WorkingDirectory))
                tool.WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? "";
            SaveTools();
            StatusMessage = $"{tool.Name} path set to: {dialog.FileName}";
        }
    }

    [RelayCommand]
    private void RemoveTool(ExternalTool? tool)
    {
        if (tool == null) return;
        Tools.Remove(tool);
        SaveTools();
        StatusMessage = $"Removed: {tool.Name}";
    }

    [RelayCommand]
    private void BrowseDcsPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select DCS.exe",
            Filter = "DCS.exe|DCS.exe|All files (*.*)|*.*",
            InitialDirectory = @"C:\Program Files\Eagle Dynamics"
        };

        if (dialog.ShowDialog() == true)
        {
            var dir = Path.GetDirectoryName(Path.GetDirectoryName(dialog.FileName));
            if (dir != null)
            {
                DcsInstallPath = dir;
                _config.DcsInstallPath = dir;
                _config.Save();
                UpdateDcsStatus();
                StatusMessage = $"DCS path set to: {dir}";
            }
        }
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        UpdateDcsStatus();
    }

    [RelayCommand]
    private void AddSuggestedTool(ExternalTool? suggestion)
    {
        if (suggestion == null) return;

        // Check if already added
        if (Tools.Any(t => t.Name == suggestion.Name))
        {
            StatusMessage = $"{suggestion.Name} is already in your tools list.";
            return;
        }

        // Ask: Browse for existing install, or open download page?
        bool hasUrl = !string.IsNullOrWhiteSpace(suggestion.ProjectUrl);

        var result = MessageBox.Show(
            hasUrl
                ? $"Do you already have {suggestion.Name} installed?\n\n" +
                  "Yes  =  Browse for the .exe on your computer\n" +
                  "No   =  Open the download page in your browser"
                : $"Browse for the {suggestion.Name} executable?",
            suggestion.Name,
            hasUrl ? MessageBoxButton.YesNoCancel : MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return;

        if (result == MessageBoxResult.No && hasUrl)
        {
            // Open download page
            try
            {
                Process.Start(new ProcessStartInfo { FileName = suggestion.ProjectUrl, UseShellExecute = true });
                StatusMessage = $"Opened {suggestion.Name} download page. Add it after installing.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open URL: {ex.Message}";
            }
            return;
        }

        // Yes / OK = browse for the exe
        var dialog = new OpenFileDialog
        {
            Title = $"Locate {suggestion.Name} executable",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            var tool = new ExternalTool
            {
                Name = suggestion.Name,
                IconHint = suggestion.IconHint,
                ProjectUrl = suggestion.ProjectUrl,
                ExePath = dialog.FileName,
                WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? "",
            };
            Tools.Add(tool);
            SaveTools();
            StatusMessage = $"{tool.Name} added: {dialog.FileName}";
        }
        else
        {
            StatusMessage = "Cancelled.";
        }
    }

    private void UpdateDcsStatus()
    {
        if (File.Exists(_config.DcsExePath))
        {
            DcsStatus = $"Installed: {_config.DcsInstallPath}";
            IsDcsRunning = Process.GetProcessesByName("DCS").Length > 0;
        }
        else
        {
            DcsStatus = "Not found - click Browse to locate DCS.exe";
        }
    }

    private void SaveTools()
    {
        _config.ExternalTools = new List<ExternalTool>(Tools);
        _config.Save();
    }
}

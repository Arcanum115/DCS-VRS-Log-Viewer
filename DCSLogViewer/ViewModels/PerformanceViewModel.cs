using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DCSLogViewer.Models;
using DCSLogViewer.Services;
using Microsoft.Win32;

namespace DCSLogViewer.ViewModels;

/// <summary>
/// ViewModel for the System and DCS Info panel.
/// Provides detailed system information, DCS path details, DXVK status, and cache management.
/// </summary>
public partial class PerformanceViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly DxvkManager _dxvk;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _cacheInfo = "";
    [ObservableProperty] private string _dxvkStatusText = "Checking...";
    [ObservableProperty] private bool _isDxvkInstalled;

    // === SYSTEM INFO PROPERTIES ===
    [ObservableProperty] private string _cpuName = "Detecting...";
    [ObservableProperty] private string _cpuCores = "";
    [ObservableProperty] private string _cpuSpeed = "";
    [ObservableProperty] private string _gpuName = "Detecting...";
    [ObservableProperty] private string _gpuVram = "";
    [ObservableProperty] private string _gpuDriver = "";
    [ObservableProperty] private string _ramTotal = "";
    [ObservableProperty] private string _ramAvailable = "";
    [ObservableProperty] private string _osName = "";
    [ObservableProperty] private string _osBuild = "";
    [ObservableProperty] private string _dlssInfo = "Not detected";
    [ObservableProperty] private string _dlaaInfo = "Not detected";
    [ObservableProperty] private string _dcsInstallDrive = "";
    [ObservableProperty] private string _dcsInstallFreeSpace = "";
    [ObservableProperty] private string _savedGamesDrive = "";
    [ObservableProperty] private string _savedGamesFreeSpace = "";
    [ObservableProperty] private string _pageFileInfo = "";
    [ObservableProperty] private string _shaderCacheSize = "";
    [ObservableProperty] private string _terrainCacheSize = "";
    [ObservableProperty] private string _totalCacheSize = "";

    public PerformanceViewModel(AppConfig config)
    {
        _config = config;
        _dxvk = new DxvkManager(config.DcsInstallPath);

        GatherAllInfo();
    }

    [RelayCommand]
    private void RefreshInfo()
    {
        StatusMessage = "Refreshing...";
        GatherAllInfo();
        StatusMessage = "System info refreshed.";
    }

    private void GatherAllInfo()
    {
        GatherCpuInfo();
        GatherGpuInfo();
        GatherMemoryInfo();
        GatherOsInfo();
        DetectDlss();
        GatherDiskInfo();
        UpdateCacheInfo();
        RefreshDxvkStatus();
    }

    // === CPU ===

    private void GatherCpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                // Clean up the name (remove extra spaces)
                while (name.Contains("  "))
                    name = name.Replace("  ", " ");
                CpuName = name;

                var cores = obj["NumberOfCores"]?.ToString() ?? "?";
                var threads = obj["NumberOfLogicalProcessors"]?.ToString() ?? "?";
                CpuCores = $"{cores} Cores / {threads} Threads";

                var mhz = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                CpuSpeed = mhz > 0 ? $"{mhz / 1000.0:F2} GHz" : "Unknown";
            }
        }
        catch
        {
            CpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown";
            CpuCores = $"{Environment.ProcessorCount} Threads";
            CpuSpeed = "";
        }
    }

    // === GPU ===

    private void GatherGpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController WHERE Availability = 3");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown";
                // Skip Microsoft Basic Display Adapter
                if (name.Contains("Microsoft Basic")) continue;

                GpuName = name;

                var vramBytes = Convert.ToUInt64(obj["AdapterRAM"] ?? 0);
                if (vramBytes > 0)
                {
                    var vramGb = vramBytes / (1024.0 * 1024 * 1024);
                    // WMI caps at 4GB due to uint32 limit — detect and show accordingly
                    GpuVram = vramGb >= 3.9 ? "4+ GB (WMI limit — likely more)" : $"{vramGb:F1} GB";
                }
                else
                {
                    GpuVram = "Unknown";
                }

                GpuDriver = obj["DriverVersion"]?.ToString() ?? "Unknown";
                break; // Take the first real GPU
            }

            // Try to get accurate VRAM from the registry (NVIDIA/AMD report correctly there)
            TryGetAccurateVram();
        }
        catch
        {
            GpuName = "Unable to detect";
            GpuVram = "";
            GpuDriver = "";
        }
    }

    private void TryGetAccurateVram()
    {
        try
        {
            // Check NVIDIA registry for accurate VRAM
            using var searcher = new ManagementObjectSearcher(
                "SELECT AdapterRAM FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%' OR Name LIKE '%AMD%' OR Name LIKE '%Radeon%'");
            foreach (ManagementObject obj in searcher.Get())
            {
                // Try the DX adapter method via DXGI if available
                break;
            }

            // Alternative: Check Display adapters in registry for qwMemorySize
            using var displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (displayKey != null)
            {
                foreach (var subKeyName in displayKey.GetSubKeyNames())
                {
                    if (!int.TryParse(subKeyName, out _)) continue;
                    using var subKey = displayKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var desc = subKey.GetValue("DriverDesc")?.ToString() ?? "";
                    if (string.IsNullOrEmpty(desc) || desc.Contains("Microsoft Basic")) continue;

                    var memSize = subKey.GetValue("HardwareInformation.qwMemorySize");
                    if (memSize is long memLong && memLong > 0)
                    {
                        var gb = memLong / (1024.0 * 1024 * 1024);
                        GpuVram = $"{gb:F0} GB";
                        return;
                    }

                    var memSizeReg = subKey.GetValue("HardwareInformation.MemorySize");
                    if (memSizeReg is byte[] memBytes && memBytes.Length >= 8)
                    {
                        var memVal = BitConverter.ToInt64(memBytes, 0);
                        if (memVal > 0)
                        {
                            var gb = memVal / (1024.0 * 1024 * 1024);
                            GpuVram = $"{gb:F0} GB";
                            return;
                        }
                    }
                }
            }
        }
        catch { /* Keep WMI value */ }
    }

    // === MEMORY ===

    private void GatherMemoryInfo()
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var totalRamBytes = gcInfo.TotalAvailableMemoryBytes;
            var totalGb = totalRamBytes / (1024.0 * 1024 * 1024);
            RamTotal = $"{totalGb:F1} GB";

            // Get available RAM via WMI
            using var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var freeKb = Convert.ToUInt64(obj["FreePhysicalMemory"] ?? 0);
                var freeGb = freeKb / (1024.0 * 1024);
                RamAvailable = $"{freeGb:F1} GB free";

                // Also get total from WMI (more accurate)
                var totalKb = Convert.ToUInt64(obj["TotalVisibleMemorySize"] ?? 0);
                if (totalKb > 0)
                {
                    totalGb = totalKb / (1024.0 * 1024);
                    RamTotal = $"{totalGb:F1} GB";
                }
            }

            // Get page file info
            using var pfSearcher = new ManagementObjectSearcher("SELECT AllocatedBaseSize, CurrentUsage FROM Win32_PageFileUsage");
            foreach (ManagementObject obj in pfSearcher.Get())
            {
                var allocMb = Convert.ToInt64(obj["AllocatedBaseSize"] ?? 0);
                var usageMb = Convert.ToInt64(obj["CurrentUsage"] ?? 0);
                var allocGb = allocMb / 1024.0;
                var usageGb = usageMb / 1024.0;
                PageFileInfo = $"{allocGb:F1} GB allocated, {usageGb:F1} GB in use";
            }
        }
        catch
        {
            var ramGb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
            RamTotal = $"{ramGb:F1} GB";
            RamAvailable = "";
            PageFileInfo = "Unknown";
        }
    }

    // === OS ===

    private void GatherOsInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                OsName = obj["Caption"]?.ToString()?.Replace("Microsoft ", "") ?? "Windows";
                var build = obj["BuildNumber"]?.ToString() ?? "";
                var version = obj["Version"]?.ToString() ?? "";
                OsBuild = $"Build {build} ({version})";
            }
        }
        catch
        {
            OsName = Environment.OSVersion.ToString();
            OsBuild = "";
        }
    }

    // === DLSS / DLAA ===

    private void DetectDlss()
    {
        var dlss = DetectDllVersion("nvngx_dlss.dll", "DLSS");
        if (dlss == null) dlss = DetectDllVersion("nvngx.dll", "DLSS");
        DlssInfo = dlss ?? "Not found in DCS";

        var dlaa = DetectDllVersion("nvngx_dlaa.dll", "DLAA");
        DlaaInfo = dlaa ?? "Not found in DCS";
    }

    private string? DetectDllVersion(string dllName, string label)
    {
        try
        {
            var binFolder = Path.Combine(_config.DcsInstallPath, "bin");
            var dllPath = Path.Combine(binFolder, dllName);
            if (!File.Exists(dllPath))
            {
                var altPath = Path.Combine(binFolder, "x64", dllName);
                if (File.Exists(altPath))
                    dllPath = altPath;
                else
                    return null;
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(dllPath);
            var version = versionInfo.FileVersion ?? versionInfo.ProductVersion;
            return version != null ? $"v{version}" : "Installed (version unknown)";
        }
        catch { return null; }
    }

    // === DISK ===

    private void GatherDiskInfo()
    {
        try
        {
            if (!string.IsNullOrEmpty(_config.DcsInstallPath))
            {
                var installRoot = Path.GetPathRoot(_config.DcsInstallPath);
                if (installRoot != null)
                {
                    var drive = new DriveInfo(installRoot);
                    DcsInstallDrive = $"{drive.Name.TrimEnd('\\')} ({drive.DriveType})";
                    DcsInstallFreeSpace = $"{drive.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1} GB free of {drive.TotalSize / (1024.0 * 1024 * 1024):F0} GB";
                }
            }

            var savedGamesPath = Path.Combine(_config.DcsSavedGamesPath, _config.DcsVariant);
            if (!string.IsNullOrEmpty(savedGamesPath))
            {
                var sgRoot = Path.GetPathRoot(savedGamesPath);
                if (sgRoot != null)
                {
                    var drive = new DriveInfo(sgRoot);
                    SavedGamesDrive = $"{drive.Name.TrimEnd('\\')} ({drive.DriveType})";
                    SavedGamesFreeSpace = $"{drive.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1} GB free of {drive.TotalSize / (1024.0 * 1024 * 1024):F0} GB";
                }
            }
        }
        catch
        {
            DcsInstallDrive = "Unknown";
            SavedGamesDrive = "Unknown";
        }
    }

    // === DXVK ===

    private void RefreshDxvkStatus()
    {
        var status = _dxvk.GetStatus();
        IsDxvkInstalled = status.State == DxvkState.Installed;
        DxvkStatusText = status.State switch
        {
            DxvkState.Installed => $"DXVK {status.Version} is installed",
            DxvkState.NotInstalled => "DXVK is not installed",
            DxvkState.DcsNotFound => "DCS install path not set",
            _ => "Unknown"
        };
    }

    // === CACHE ===

    [RelayCommand]
    private void ClearShaderCache()
    {
        int deleted = 0;
        var savedGamesBase = Path.Combine(_config.DcsSavedGamesPath, _config.DcsVariant);

        string[] cacheDirs = ["fxo", "metashaders2"];
        foreach (var dir in cacheDirs)
        {
            var path = Path.Combine(savedGamesBase, dir);
            if (Directory.Exists(path))
            {
                try
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    deleted += files.Length;
                    Directory.Delete(path, recursive: true);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error clearing {dir}: {ex.Message}";
                    return;
                }
            }
        }

        UpdateCacheInfo();
        StatusMessage = deleted > 0
            ? $"Cleared shader cache ({deleted} files removed). DCS will rebuild shaders on next launch."
            : "Shader cache folders not found or already clean.";
    }

    [RelayCommand]
    private void ClearTerrainCache()
    {
        var savedGamesBase = Path.Combine(_config.DcsSavedGamesPath, _config.DcsVariant);
        var terrainPath = Path.Combine(savedGamesBase, "terrain");
        int deleted = 0;

        if (Directory.Exists(terrainPath))
        {
            try
            {
                var files = Directory.GetFiles(terrainPath, "*", SearchOption.AllDirectories);
                deleted = files.Length;
                Directory.Delete(terrainPath, recursive: true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error clearing terrain cache: {ex.Message}";
                return;
            }
        }

        UpdateCacheInfo();
        StatusMessage = deleted > 0
            ? $"Cleared terrain cache ({deleted} files removed)."
            : "Terrain cache not found or already clean.";
    }

    private void UpdateCacheInfo()
    {
        var savedGamesBase = Path.Combine(_config.DcsSavedGamesPath, _config.DcsVariant);
        long shaderBytes = 0, terrainBytes = 0;
        int shaderFiles = 0, terrainFiles = 0;

        // Shader cache (fxo + metashaders2)
        foreach (var dir in new[] { "fxo", "metashaders2" })
        {
            var path = Path.Combine(savedGamesBase, dir);
            if (Directory.Exists(path))
            {
                try
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    shaderFiles += files.Length;
                    foreach (var f in files)
                        shaderBytes += new FileInfo(f).Length;
                }
                catch { }
            }
        }

        // Terrain cache
        var terrainPath = Path.Combine(savedGamesBase, "terrain");
        if (Directory.Exists(terrainPath))
        {
            try
            {
                var files = Directory.GetFiles(terrainPath, "*", SearchOption.AllDirectories);
                terrainFiles = files.Length;
                foreach (var f in files)
                    terrainBytes += new FileInfo(f).Length;
            }
            catch { }
        }

        ShaderCacheSize = FormatSize(shaderBytes, shaderFiles);
        TerrainCacheSize = FormatSize(terrainBytes, terrainFiles);
        TotalCacheSize = FormatSize(shaderBytes + terrainBytes, shaderFiles + terrainFiles);
    }

    private static string FormatSize(long bytes, int files)
    {
        if (files == 0) return "Empty";
        var mb = bytes / (1024.0 * 1024);
        if (mb >= 1024)
            return $"{mb / 1024.0:F2} GB ({files:N0} files)";
        return $"{mb:F1} MB ({files:N0} files)";
    }
}

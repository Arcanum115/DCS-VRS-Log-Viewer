using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace DCSLogViewer.Services;

/// <summary>
/// Manages DXVK installation for DCS World.
/// DXVK translates DirectX 11 calls to Vulkan, which can improve performance
/// especially on AMD GPUs or when CPU-bound.
/// </summary>
public class DxvkManager
{
    private static readonly string[] DxvkDlls = ["d3d11.dll", "dxgi.dll"];
    private static readonly string DxvkDownloadUrl =
        "https://github.com/doitsujin/dxvk/releases/download/v2.5.3/dxvk-2.5.3.tar.gz";
    private static readonly string DxvkVersion = "2.5.3";

    private readonly string _dcsInstallPath;

    public DxvkManager(string dcsInstallPath)
    {
        _dcsInstallPath = dcsInstallPath;
    }

    /// <summary>
    /// The DCS bin folder where DCS.exe lives.
    /// </summary>
    public string DcsBinFolder => Path.Combine(_dcsInstallPath, "bin");

    /// <summary>
    /// Checks if DXVK DLLs are currently installed in the DCS bin folder.
    /// </summary>
    public DxvkStatus GetStatus()
    {
        if (string.IsNullOrWhiteSpace(_dcsInstallPath) || !Directory.Exists(DcsBinFolder))
            return new DxvkStatus { State = DxvkState.DcsNotFound };

        bool anyPresent = false;
        bool backupsExist = false;

        foreach (var dll in DxvkDlls)
        {
            var dllPath = Path.Combine(DcsBinFolder, dll);
            var backupPath = dllPath + ".original";

            if (File.Exists(dllPath) && File.Exists(backupPath))
            {
                anyPresent = true;
                backupsExist = true;
            }
        }

        if (anyPresent && backupsExist)
            return new DxvkStatus { State = DxvkState.Installed, Version = DxvkVersion };
        else
            return new DxvkStatus { State = DxvkState.NotInstalled };
    }

    /// <summary>
    /// Installs DXVK by copying DLLs from a local extracted folder into the DCS bin folder.
    /// Backs up the original DLLs first.
    /// </summary>
    public async Task<string> InstallFromLocalAsync(string dxvkExtractedFolder)
    {
        if (!Directory.Exists(DcsBinFolder))
            return $"DCS bin folder not found: {DcsBinFolder}";

        // Look for x64 DLLs in the extracted folder
        var x64Folder = FindX64Folder(dxvkExtractedFolder);
        if (x64Folder == null)
            return "Could not find x64 DLL folder in the DXVK archive. Expected a folder containing d3d11.dll and dxgi.dll.";

        try
        {
            foreach (var dll in DxvkDlls)
            {
                var sourcePath = Path.Combine(x64Folder, dll);
                var destPath = Path.Combine(DcsBinFolder, dll);
                var backupPath = destPath + ".original";

                if (!File.Exists(sourcePath))
                    return $"DXVK DLL not found: {sourcePath}";

                // Backup original if it exists and no backup yet
                if (File.Exists(destPath) && !File.Exists(backupPath))
                    File.Copy(destPath, backupPath, overwrite: false);

                // Copy DXVK DLL
                File.Copy(sourcePath, destPath, overwrite: true);
            }

            return "DXVK installed successfully! Restart DCS for changes to take effect.";
        }
        catch (Exception ex)
        {
            return $"Installation failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Downloads DXVK from GitHub, extracts, and installs.
    /// </summary>
    public async Task<string> DownloadAndInstallAsync(Action<string>? progress = null)
    {
        if (!Directory.Exists(DcsBinFolder))
            return $"DCS bin folder not found: {DcsBinFolder}";

        var tempDir = Path.Combine(Path.GetTempPath(), "DCSManager_DXVK");
        var zipPath = Path.Combine(tempDir, $"dxvk-{DxvkVersion}.tar.gz");

        try
        {
            Directory.CreateDirectory(tempDir);

            // Download
            progress?.Invoke("Downloading DXVK...");
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DCSManager/1.0");
            var response = await httpClient.GetAsync(DxvkDownloadUrl);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(zipPath);
            await response.Content.CopyToAsync(fileStream);
            fileStream.Close();

            progress?.Invoke("Extracting DXVK...");

            // Extract tar.gz (two-step: gzip then tar)
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);

            // Use GZipStream to decompress, then extract tar
            var tarPath = zipPath.Replace(".gz", "");
            await using (var gzStream = File.OpenRead(zipPath))
            await using (var decompressed = new GZipStream(gzStream, CompressionMode.Decompress))
            await using (var tarFile = File.Create(tarPath))
            {
                await decompressed.CopyToAsync(tarFile);
            }

            // Simple tar extraction for what we need
            ExtractTarDlls(tarPath, extractDir);

            progress?.Invoke("Installing DXVK DLLs...");
            var result = await InstallFromLocalAsync(extractDir);

            // Cleanup
            try { Directory.Delete(tempDir, recursive: true); } catch { }

            return result;
        }
        catch (Exception ex)
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
            return $"Download failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes DXVK by restoring original DLLs from backups.
    /// </summary>
    public string Uninstall()
    {
        if (!Directory.Exists(DcsBinFolder))
            return $"DCS bin folder not found: {DcsBinFolder}";

        try
        {
            int restored = 0;
            foreach (var dll in DxvkDlls)
            {
                var dllPath = Path.Combine(DcsBinFolder, dll);
                var backupPath = dllPath + ".original";

                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, dllPath, overwrite: true);
                    File.Delete(backupPath);
                    restored++;
                }
                else if (File.Exists(dllPath))
                {
                    // No backup exists, just delete the DXVK dll
                    File.Delete(dllPath);
                    restored++;
                }
            }

            return restored > 0
                ? "DXVK removed. Original DLLs restored. Restart DCS for changes to take effect."
                : "No DXVK files found to remove.";
        }
        catch (Exception ex)
        {
            return $"Uninstall failed: {ex.Message}";
        }
    }

    private string? FindX64Folder(string rootFolder)
    {
        // Look for d3d11.dll in common DXVK folder structures
        // Typical: dxvk-X.Y.Z/x64/d3d11.dll or just x64/d3d11.dll
        var candidates = new[]
        {
            rootFolder,
            Path.Combine(rootFolder, "x64"),
        };

        // Also search recursively for the first x64 folder
        foreach (var dir in Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(dir).Equals("x64", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Append(dir).ToArray();
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "d3d11.dll")))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Very simple tar file extractor that pulls just the DLLs we need.
    /// </summary>
    private void ExtractTarDlls(string tarPath, string outputDir)
    {
        using var stream = File.OpenRead(tarPath);
        var buffer = new byte[512];

        while (true)
        {
            // Read header block
            int bytesRead = stream.Read(buffer, 0, 512);
            if (bytesRead < 512) break;

            // Check for end-of-archive (two zero blocks)
            if (buffer.All(b => b == 0)) break;

            // Extract filename from header (bytes 0-99)
            var nameBytes = buffer.Take(100).TakeWhile(b => b != 0).ToArray();
            var name = System.Text.Encoding.ASCII.GetString(nameBytes).Trim();

            // Extract file size from header (bytes 124-135, octal)
            var sizeStr = System.Text.Encoding.ASCII.GetString(buffer, 124, 12).Trim('\0', ' ');
            long fileSize = 0;
            if (!string.IsNullOrWhiteSpace(sizeStr))
                fileSize = Convert.ToInt64(sizeStr, 8);

            // Calculate blocks to skip (512-byte aligned)
            long blocks = (fileSize + 511) / 512;

            // Check if this is a DLL we want (in x64 folder)
            var fileName = Path.GetFileName(name);
            bool isTarget = name.Contains("x64/") &&
                            DxvkDlls.Contains(fileName, StringComparer.OrdinalIgnoreCase);

            if (isTarget && fileSize > 0)
            {
                var outPath = Path.Combine(outputDir, "x64");
                Directory.CreateDirectory(outPath);
                var fullOutPath = Path.Combine(outPath, fileName);

                using var outFile = File.Create(fullOutPath);
                long remaining = fileSize;
                var readBuf = new byte[8192];
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(readBuf.Length, remaining);
                    int read = stream.Read(readBuf, 0, toRead);
                    if (read == 0) break;
                    outFile.Write(readBuf, 0, read);
                    remaining -= read;
                }

                // Skip padding to next 512-byte boundary
                long totalRead2 = fileSize;
                long paddedSize = blocks * 512;
                long skip = paddedSize - totalRead2;
                if (skip > 0) stream.Seek(skip, SeekOrigin.Current);
            }
            else
            {
                // Skip file data
                if (blocks > 0)
                    stream.Seek(blocks * 512, SeekOrigin.Current);
            }
        }
    }
}

public enum DxvkState
{
    NotInstalled,
    Installed,
    DcsNotFound
}

public class DxvkStatus
{
    public DxvkState State { get; init; }
    public string Version { get; init; } = "";
}

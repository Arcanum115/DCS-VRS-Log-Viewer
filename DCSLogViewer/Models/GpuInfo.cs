namespace DCSLogViewer.Models;

/// <summary>
/// Represents a single GPU/graphics adapter detected on the system.
/// </summary>
public class GpuInfo
{
    public string Name { get; set; } = "Unknown";
    public string Vram { get; set; } = "Unknown";
    public string DriverVersion { get; set; } = "Unknown";
    public bool IsPrimary { get; set; }
    public bool IsDedicated { get; set; }

    // GPU vendor type
    public GpuVendor Vendor { get; set; } = GpuVendor.Unknown;

    // NVIDIA-specific upscaling
    public string DlssInfo { get; set; } = "Not detected";
    public string DlaaInfo { get; set; } = "Not detected";

    // AMD-specific upscaling
    public string FsrInfo { get; set; } = "Not detected";
    public string RsrInfo { get; set; } = "Not detected";

    // Intel-specific upscaling
    public string XessInfo { get; set; } = "Not detected";

    /// <summary>
    /// Label for the first upscaling tech column based on vendor.
    /// </summary>
    public string UpscaleLabel1 => Vendor switch
    {
        GpuVendor.Nvidia => "DLSS",
        GpuVendor.Amd => "FSR",
        GpuVendor.Intel => "XeSS",
        _ => "Upscaling"
    };

    /// <summary>
    /// Label for the second upscaling tech column based on vendor.
    /// </summary>
    public string UpscaleLabel2 => Vendor switch
    {
        GpuVendor.Nvidia => "DLAA",
        GpuVendor.Amd => "RSR",
        GpuVendor.Intel => "",
        _ => ""
    };

    /// <summary>
    /// Value for the first upscaling tech based on vendor.
    /// </summary>
    public string UpscaleValue1 => Vendor switch
    {
        GpuVendor.Nvidia => DlssInfo,
        GpuVendor.Amd => FsrInfo,
        GpuVendor.Intel => XessInfo,
        _ => "N/A"
    };

    /// <summary>
    /// Value for the second upscaling tech based on vendor.
    /// </summary>
    public string UpscaleValue2 => Vendor switch
    {
        GpuVendor.Nvidia => DlaaInfo,
        GpuVendor.Amd => RsrInfo,
        GpuVendor.Intel => "",
        _ => ""
    };

    public bool HasSecondUpscale => !string.IsNullOrEmpty(UpscaleLabel2);
}

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

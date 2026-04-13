namespace DCSLogViewer.Models;

/// <summary>
/// Predefined performance optimization presets for DCS.
/// Each preset configures graphics, terrain, and system settings.
/// </summary>
public class PerformancePreset
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string TargetHardware { get; init; } = "";

    // Graphics
    public string VisibRange { get; init; } = "High";
    public int Textures { get; init; } = 1;
    public string TerrainTextures { get; init; } = "max";
    public int Water { get; init; } = 1;
    public int Shadows { get; init; } = 2;
    public int MSAA { get; init; } = 1;
    public int SSAA { get; init; }
    public int PreloadRadius { get; init; } = 100000;
    public int ClutterMaxDistance { get; init; } = 1000;
    public double ForestDetailsFactor { get; init; } = 0.5;
    public double ForestDistanceFactor { get; init; } = 0.5;
    public int LensEffects { get; init; }
    public int HeatBlur { get; init; }
    public int HDR { get; init; } = 1;
    public int CockpitGI { get; init; }
    public int SSAO { get; init; }
    public int SSLR { get; init; }
    public int ChimneySmokeAmount { get; init; } = 3;
    public int MotionBlur { get; init; }
    public bool ShadowTree { get; init; }
    public int FlatTerrainShadows { get; init; }
    public bool Sync { get; init; }
    public int DOF { get; init; }

    // Autoexec / system tweaks
    public int? MaxFps { get; init; }
    public bool TerrainNormals4k { get; init; }

    public static readonly PerformancePreset[] AllPresets =
    [
        new()
        {
            Name = "Potato Mode",
            Description = "Maximum FPS on low-end hardware. Strips everything non-essential.",
            TargetHardware = "Older GPUs (GTX 1060 / RX 580 or lower), 8GB RAM",
            VisibRange = "Low",
            Textures = 0,
            TerrainTextures = "min",
            Water = 0,
            Shadows = 0,
            MSAA = 0,
            SSAA = 0,
            PreloadRadius = 50000,
            ClutterMaxDistance = 0,
            ForestDetailsFactor = 0,
            ForestDistanceFactor = 0,
            LensEffects = 0,
            HeatBlur = 0,
            HDR = 0,
            CockpitGI = 0,
            SSAO = 0,
            SSLR = 0,
            ChimneySmokeAmount = 0,
            MotionBlur = 0,
            ShadowTree = false,
            FlatTerrainShadows = 0,
            Sync = false,
            DOF = 0,
        },
        new()
        {
            Name = "Competitive / MP",
            Description = "Optimized for multiplayer. Good visibility, smooth frames, minimal eye candy.",
            TargetHardware = "Mid-range GPUs (RTX 3060 / RX 6700 XT), 16GB RAM",
            VisibRange = "Ultra",
            Textures = 1,
            TerrainTextures = "max",
            Water = 1,
            Shadows = 2,
            MSAA = 1,
            SSAA = 0,
            PreloadRadius = 100000,
            ClutterMaxDistance = 800,
            ForestDetailsFactor = 0.5,
            ForestDistanceFactor = 0.5,
            LensEffects = 0,
            HeatBlur = 0,
            HDR = 1,
            CockpitGI = 0,
            SSAO = 0,
            SSLR = 0,
            ChimneySmokeAmount = 1,
            MotionBlur = 0,
            ShadowTree = false,
            FlatTerrainShadows = 0,
            Sync = false,
            DOF = 0,
        },
        new()
        {
            Name = "Balanced",
            Description = "Good balance of visuals and performance. Recommended starting point.",
            TargetHardware = "RTX 3070 / RX 6800 XT, 32GB RAM",
            VisibRange = "Ultra",
            Textures = 2,
            TerrainTextures = "max",
            Water = 2,
            Shadows = 3,
            MSAA = 2,
            SSAA = 0,
            PreloadRadius = 120000,
            ClutterMaxDistance = 1500,
            ForestDetailsFactor = 0.8,
            ForestDistanceFactor = 0.8,
            LensEffects = 1,
            HeatBlur = 1,
            HDR = 1,
            CockpitGI = 1,
            SSAO = 1,
            SSLR = 0,
            ChimneySmokeAmount = 3,
            MotionBlur = 0,
            ShadowTree = false,
            FlatTerrainShadows = 1,
            Sync = false,
            DOF = 0,
        },
        new()
        {
            Name = "Eye Candy",
            Description = "Maximum visual quality. For high-end systems and screenshots.",
            TargetHardware = "RTX 4080+ / RX 7900 XTX+, 32GB+ RAM",
            VisibRange = "Ultra",
            Textures = 2,
            TerrainTextures = "max",
            Water = 2,
            Shadows = 4,
            MSAA = 4,
            SSAA = 1,
            PreloadRadius = 200000,
            ClutterMaxDistance = 3000,
            ForestDetailsFactor = 1.0,
            ForestDistanceFactor = 1.0,
            LensEffects = 3,
            HeatBlur = 2,
            HDR = 1,
            CockpitGI = 1,
            SSAO = 2,
            SSLR = 1,
            ChimneySmokeAmount = 5,
            MotionBlur = 1,
            ShadowTree = true,
            FlatTerrainShadows = 1,
            Sync = false,
            DOF = 1,
        },
        new()
        {
            Name = "VR Optimized",
            Description = "Tuned for VR headsets. Prioritizes frame timing consistency.",
            TargetHardware = "RTX 3080+ / RX 6900 XT+, 32GB RAM, any VR headset",
            VisibRange = "High",
            Textures = 1,
            TerrainTextures = "max",
            Water = 1,
            Shadows = 2,
            MSAA = 0,
            SSAA = 0,
            PreloadRadius = 80000,
            ClutterMaxDistance = 600,
            ForestDetailsFactor = 0.4,
            ForestDistanceFactor = 0.4,
            LensEffects = 0,
            HeatBlur = 0,
            HDR = 1,
            CockpitGI = 1,
            SSAO = 0,
            SSLR = 0,
            ChimneySmokeAmount = 0,
            MotionBlur = 0,
            ShadowTree = false,
            FlatTerrainShadows = 0,
            Sync = false,
            DOF = 0,
            MaxFps = 0,
        },
    ];
}

using Godot;

namespace km;

public enum WindowModeOpt { Windowed, Borderless, Fullscreen }

/// <summary>Uživatelské nastavení – ukládá se do user://settings.cfg a hned se aplikuje.</summary>
public static class Settings
{
    const string FilePath = "user://settings.cfg";

    public static readonly Vector2I[] Resolutions =
    {
        new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440),
    };
    public static readonly int[] FpsLimits = { 0, 30, 60, 120, 144 };
    public static readonly float[] UiScales = { 0.85f, 1f, 1.15f, 1.3f, 1.5f };

    public static int ResolutionIndex = 2;
    public static WindowModeOpt Mode = WindowModeOpt.Windowed;
    public static bool VSync = true;
    public static int FpsIndex;
    public static int MsaaIndex = 2;          // 0 vyp, 1 = 2×, 2 = 4×, 3 = 8×
    public static int UiScaleIndex = 1;
    public static bool EdgeScroll = true;
    public static float ScrollSpeed = 1f;
    public static bool PostEffects = true;

    public static float UiScale => UiScales[Mathf.Clamp(UiScaleIndex, 0, UiScales.Length - 1)];

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) != Error.Ok) return;
        ResolutionIndex = Mathf.Clamp((int)cfg.GetValue("video", "resolution", ResolutionIndex), 0, Resolutions.Length - 1);
        Mode = (WindowModeOpt)(int)cfg.GetValue("video", "mode", (int)Mode);
        VSync = (bool)cfg.GetValue("video", "vsync", VSync);
        FpsIndex = Mathf.Clamp((int)cfg.GetValue("video", "fps", FpsIndex), 0, FpsLimits.Length - 1);
        MsaaIndex = Mathf.Clamp((int)cfg.GetValue("video", "msaa", MsaaIndex), 0, 3);
        PostEffects = (bool)cfg.GetValue("video", "post", PostEffects);
        UiScaleIndex = Mathf.Clamp((int)cfg.GetValue("ui", "scale", UiScaleIndex), 0, UiScales.Length - 1);
        EdgeScroll = (bool)cfg.GetValue("controls", "edge_scroll", EdgeScroll);
        ScrollSpeed = (float)cfg.GetValue("controls", "scroll_speed", ScrollSpeed);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("video", "resolution", ResolutionIndex);
        cfg.SetValue("video", "mode", (int)Mode);
        cfg.SetValue("video", "vsync", VSync);
        cfg.SetValue("video", "fps", FpsIndex);
        cfg.SetValue("video", "msaa", MsaaIndex);
        cfg.SetValue("video", "post", PostEffects);
        cfg.SetValue("ui", "scale", UiScaleIndex);
        cfg.SetValue("controls", "edge_scroll", EdgeScroll);
        cfg.SetValue("controls", "scroll_speed", ScrollSpeed);
        cfg.Save(FilePath);
    }

    public static void Apply(Window root)
    {
        switch (Mode)
        {
            case WindowModeOpt.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
            case WindowModeOpt.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            default:
                if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                var res = Resolutions[ResolutionIndex];
                var screen = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
                // okno nesmí být větší než obrazovka
                res = new Vector2I(Mathf.Min(res.X, screen.Size.X), Mathf.Min(res.Y, screen.Size.Y));
                DisplayServer.WindowSetSize(res);
                DisplayServer.WindowSetPosition(screen.Position + (screen.Size - res) / 2);
                break;
        }
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = FpsLimits[FpsIndex];
        root.Msaa2D = (Viewport.Msaa)MsaaIndex;
    }
}

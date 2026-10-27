using System;
using Godot;

namespace km;

/// <summary>Nastavení obrazu a ovládání. Změny se hned aplikují a uloží.</summary>
public partial class SettingsPanel : VBoxContainer
{
    public event Action? Back;
    OptionButton resolution = null!;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 10);
        AddChild(MenuStyle.Heading("Nastavení"));

        AddChild(MenuStyle.Section("Obraz"));
        var video = Grid();
        var mode = Option(new[] { "V okně", "Celá obrazovka (bez okrajů)", "Celá obrazovka (exkluzivní)" }, (int)Settings.Mode, i =>
        {
            Settings.Mode = (WindowModeOpt)i;
            resolution.Disabled = Settings.Mode != WindowModeOpt.Windowed;
        });
        Row(video, "Režim zobrazení", mode);
        var resNames = Array.ConvertAll(Settings.Resolutions, r => $"{r.X} × {r.Y}");
        resolution = Option(resNames, Settings.ResolutionIndex, i => Settings.ResolutionIndex = i);
        resolution.Disabled = Settings.Mode != WindowModeOpt.Windowed;
        Row(video, "Rozlišení okna", resolution);
        Row(video, "Vertikální synchronizace", Check(Settings.VSync, v => Settings.VSync = v));
        Row(video, "Limit snímků", Option(new[] { "Bez limitu", "30 FPS", "60 FPS", "120 FPS", "144 FPS" }, Settings.FpsIndex, i => Settings.FpsIndex = i));
        Row(video, "Vyhlazování hran", Option(new[] { "Vypnuto", "2×", "4×", "8×" }, Settings.MsaaIndex, i => Settings.MsaaIndex = i));
        Row(video, "Barevné doladění a vinětace", Check(Settings.PostEffects, v => Settings.PostEffects = v));
        AddChild(video);

        AddChild(MenuStyle.Section("Rozhraní a ovládání"));
        var ui = Grid();
        var scales = Array.ConvertAll(Settings.UiScales, s => $"{Mathf.RoundToInt(s * 100)} %");
        Row(ui, "Velikost rozhraní", Option(scales, Settings.UiScaleIndex, i => Settings.UiScaleIndex = i));
        Row(ui, "Posun kamery u okraje obrazovky", Check(Settings.EdgeScroll, v => Settings.EdgeScroll = v));
        var speed = new HSlider
        {
            MinValue = 0.4, MaxValue = 2.5, Step = 0.1, Value = Settings.ScrollSpeed,
            CustomMinimumSize = new Vector2(280, 24), SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        speed.ValueChanged += v =>
        {
            Settings.ScrollSpeed = (float)v;
            Settings.Save();
        };
        Row(ui, "Rychlost posunu kamery", speed);
        AddChild(ui);

        var back = MenuStyle.BigButton("Zpět");
        back.Pressed += () => Back?.Invoke();
        AddChild(back);
    }

    static GridContainer Grid()
    {
        var g = new GridContainer { Columns = 2 };
        g.AddThemeConstantOverride("h_separation", 24);
        g.AddThemeConstantOverride("v_separation", 8);
        return g;
    }

    static void Row(GridContainer g, string label, Control control)
    {
        var l = new Label { Text = label, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(250, 0) };
        g.AddChild(l);
        g.AddChild(control);
    }

    void Changed()
    {
        Settings.Apply(GetTree().Root);
        Settings.Save();
    }

    OptionButton Option(string[] items, int selected, Action<int> set)
    {
        var o = new OptionButton { CustomMinimumSize = new Vector2(280, 34), FocusMode = FocusModeEnum.None };
        foreach (var it in items) o.AddItem(it);
        o.Selected = selected;
        o.ItemSelected += i =>
        {
            set((int)i);
            Changed();
        };
        return o;
    }

    CheckBox Check(bool value, Action<bool> set)
    {
        var c = new CheckBox { ButtonPressed = value, Text = value ? "Zapnuto" : "Vypnuto", FocusMode = FocusModeEnum.None };
        c.Toggled += v =>
        {
            c.Text = v ? "Zapnuto" : "Vypnuto";
            set(v);
            Changed();
        };
        return c;
    }
}

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace km;

/// <summary>
/// Rozhraní ve stylu dřevěných desek: horní lišta se surovinami, lidmi a rychlostí,
/// spodní lišta s minimapou, stavbami, lidmi a informacemi o vybrané budově.
/// </summary>
public partial class Hud : CanvasLayer
{
    Control root = null!;
    readonly Dictionary<ResourceType, Label> resLabels = new();
    Label serfLabel = null!, laborerLabel = null!, craftLabel = null!;
    readonly List<Button> speedButtons = new();
    Button pauseButton = null!;
    readonly List<(BuildButton btn, ToolMode mode, BuildingDef? def)> toolButtons = new();

    // informační panel
    Control infoBox = null!, tipsBox = null!;
    Label infoTitle = null!, infoState = null!, infoWorker = null!;
    ProgressBar infoProgress = null!;
    readonly Dictionary<ResourceType, (Control chip, Label label)> infoChips = new();
    float timer;

    public override void _Ready()
    {
        root = new UiRoot();
        AddChild(root);

        BuildTopBar();
        BuildBottomBar();
    }

    // ---------------------------------------------------------------- horní lišta

    void BuildTopBar()
    {
        var top = new PanelContainer();
        top.AddThemeStyleboxOverride("panel", UiArt.Bar);
        root.AddChild(top);
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 18);
        top.AddChild(hb);

        foreach (var r in Res.All)
            resLabels[r] = Chip(hb, IconPainter.ForResource(r), $"{Res.Name(r)} ve skladech", 30).label;
        hb.AddChild(new VSeparator());
        serfLabel = Chip(hb, IconKind.Serf, "Nosiči (v závorce volní)", 30).label;
        laborerLabel = Chip(hb, IconKind.Laborer, "Stavitelé (v závorce volní)", 30).label;
        craftLabel = Chip(hb, IconKind.Craftsman, "Řemeslníci", 30).label;

        hb.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var title = new Label { Text = "Rychlost", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeColorOverride("font_color", UiArt.Gold);
        hb.AddChild(title);
        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 3);
        hb.AddChild(speeds);
        pauseButton = SmallButton(speeds, "II", "Pauza (mezerník)");
        pauseButton.Pressed += () => Main.Instance.TogglePause();
        for (int i = 0; i < Main.Speeds.Length; i++)
        {
            int idx = i;
            var b = SmallButton(speeds, $"{Main.Speeds[i]}×", "Rychlost hry (+ / −)");
            b.Pressed += () =>
            {
                if (Main.Instance.Paused) Main.Instance.TogglePause();
                Main.Instance.SetSpeed(idx);
            };
            speedButtons.Add(b);
        }
    }

    static Button SmallButton(Control parent, string text, string tip)
    {
        var b = new Button
        {
            Text = text, TooltipText = tip, ToggleMode = true, FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(40, 30),
        };
        b.AddThemeFontOverride("font", UiArt.SerifBold);
        b.AddThemeFontSizeOverride("font_size", 14);
        parent.AddChild(b);
        return b;
    }

    static (Control chip, Label label) Chip(Control parent, IconKind icon, string tip, float iconSize)
    {
        var hb = new HBoxContainer { TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Pass };
        hb.AddThemeConstantOverride("separation", 4);
        hb.AddChild(new UiIcon { Kind = icon, CustomMinimumSize = new Vector2(iconSize, iconSize) });
        var l = new Label { VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(28, 0) };
        l.AddThemeFontOverride("font", UiArt.SerifBold);
        l.AddThemeFontSizeOverride("font_size", 17);
        hb.AddChild(l);
        parent.AddChild(hb);
        return (hb, l);
    }

    // ---------------------------------------------------------------- spodní lišta

    void BuildBottomBar()
    {
        var bar = new PanelContainer();
        root.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bar.GrowVertical = Control.GrowDirection.Begin;

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 10);
        bar.AddChild(hb);

        // minimapa v zapuštěném rámu
        var mapFrame = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        mapFrame.AddThemeStyleboxOverride("panel", UiArt.Inset);
        mapFrame.AddChild(new Minimap());
        hb.AddChild(mapFrame);
        hb.AddChild(new VSeparator());

        // stavby
        var build = Section(hb, "Stavby");
        AddTool(build, IconKind.Road, null, "Silnice", "R",
            "Silnice\nNaplánuje cestu (táhni myší). Po silnici se chodí\nrychleji a hledání cest ji upřednostňuje.", ToolMode.Road);
        int i = 1;
        foreach (var d in BuildingDef.Buildable)
            AddTool(build, IconKind.Building, d, d.Name, (i++).ToString(),
                $"{d.Name}\n{d.Description}\nCena: {d.CostWood}× dřevo, {d.CostStone}× kámen", ToolMode.Build);
        AddTool(build, IconKind.Demolish, null, "Zbourat", "X", "Zbourat\nZbourá budovu, silnici nebo zruší plán.", ToolMode.Demolish);
        hb.AddChild(new VSeparator());

        // lidé
        var people = Section(hb, "Lidé");
        var serf = new BuildButton { Kind = IconKind.Serf, Caption = "+ Nosič", TooltipText = "Přidá nosiče u skladu." };
        serf.Pressed += () => Main.Instance.Recruit(UnitKind.Serf);
        people.AddChild(serf);
        var lab = new BuildButton { Kind = IconKind.Laborer, Caption = "+ Stavitel", TooltipText = "Přidá stavitele u skladu." };
        lab.Pressed += () => Main.Instance.Recruit(UnitKind.Laborer);
        people.AddChild(lab);
        hb.AddChild(new VSeparator());

        // informace
        var info = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(250, 0) };
        info.AddThemeConstantOverride("margin_left", 4);
        info.AddThemeConstantOverride("margin_right", 6);
        hb.AddChild(info);
        BuildInfo(info);
    }

    static HBoxContainer Section(Control parent, string title)
    {
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 2);
        parent.AddChild(vb);
        var l = new Label { Text = title };
        l.AddThemeColorOverride("font_color", UiArt.Gold);
        l.AddThemeFontOverride("font", UiArt.SerifBold);
        l.AddThemeFontSizeOverride("font_size", 15);
        vb.AddChild(l);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        vb.AddChild(row);
        return row;
    }

    void AddTool(Control parent, IconKind icon, BuildingDef? def, string caption, string hotkey, string tip, ToolMode mode)
    {
        var b = new BuildButton
        {
            Kind = icon, Def = def, Caption = caption, Hotkey = hotkey, TooltipText = tip, ToggleMode = true,
        };
        b.Pressed += () => Main.Instance.SelectTool(mode, def);
        parent.AddChild(b);
        toolButtons.Add((b, mode, def));
    }

    void BuildInfo(Control parent)
    {
        tipsBox = new VBoxContainer();
        parent.AddChild(tipsBox);
        var tipsTitle = new Label { Text = "Vítej, vladaři" };
        tipsTitle.AddThemeColorOverride("font_color", UiArt.Gold);
        tipsTitle.AddThemeFontOverride("font", UiArt.SerifBold);
        tipsTitle.AddThemeFontSizeOverride("font_size", 18);
        tipsBox.AddChild(tipsTitle);
        var tips = new Label
        {
            Text = "Kliknutím vyber budovu.\n" +
                   "WASD / šipky, prostřední tlačítko – posun\n" +
                   "Kolečko – přiblížení · Pravé tlačítko – zrušit\n" +
                   "Mezerník – pauza · P – zobrazit cesty",
        };
        tips.AddThemeFontSizeOverride("font_size", 14);
        tips.AddThemeColorOverride("font_color", new Color(0.92f, 0.85f, 0.70f));
        tipsBox.AddChild(tips);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 4);
        infoBox = vb;
        parent.AddChild(vb);

        var head = new HBoxContainer();
        vb.AddChild(head);
        infoTitle = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        infoTitle.AddThemeColorOverride("font_color", UiArt.Gold);
        infoTitle.AddThemeFontOverride("font", UiArt.SerifBold);
        infoTitle.AddThemeFontSizeOverride("font_size", 20);
        head.AddChild(infoTitle);
        var demolish = new Button { Text = "Zbourat", FocusMode = Control.FocusModeEnum.None, TooltipText = "Zbourá vybranou budovu." };
        demolish.AddThemeFontSizeOverride("font_size", 13);
        demolish.Pressed += () =>
        {
            if (Main.Instance.Selected is { } b) Main.Instance.Demolish(b);
        };
        head.AddChild(demolish);

        infoState = new Label();
        infoState.AddThemeFontSizeOverride("font_size", 14);
        vb.AddChild(infoState);
        infoProgress = new ProgressBar { CustomMinimumSize = new Vector2(0, 16), MaxValue = 100, ShowPercentage = false };
        vb.AddChild(infoProgress);

        var chips = new HBoxContainer();
        chips.AddThemeConstantOverride("separation", 16);
        vb.AddChild(chips);
        foreach (var r in Res.All)
            infoChips[r] = Chip(chips, IconPainter.ForResource(r), Res.Name(r), 26);

        infoWorker = new Label();
        infoWorker.AddThemeFontSizeOverride("font_size", 14);
        infoWorker.AddThemeColorOverride("font_color", new Color(0.92f, 0.85f, 0.70f));
        vb.AddChild(infoWorker);
    }

    // ---------------------------------------------------------------- obnova

    public override void _Process(double delta)
    {
        var m = Main.Instance;
        foreach (var (btn, mode, def) in toolButtons)
            btn.SetPressedNoSignal(m.Tool == mode && m.BuildDef == def);
        pauseButton.SetPressedNoSignal(m.Paused);
        for (int i = 0; i < speedButtons.Count; i++)
            speedButtons[i].SetPressedNoSignal(!m.Paused && m.SpeedIndex == i);

        RefreshInfo();

        timer -= (float)delta;
        if (timer > 0f && !m.Paused) return;
        timer = 0.2f;
        var eco = m.Eco;
        foreach (var r in Res.All) resLabels[r].Text = eco.TotalInStores(r).ToString();
        serfLabel.Text = $"{eco.Serfs.Count} ({eco.Serfs.Count(s => s.Job == null)})";
        laborerLabel.Text = $"{eco.Laborers.Count} ({eco.Laborers.Count(l => l.Order == null)})";
        craftLabel.Text = eco.Workers.Count.ToString();
    }

    void RefreshInfo()
    {
        var b = Main.Instance.Selected;
        bool show = b is { Removed: false };
        infoBox.Visible = show;
        tipsBox.Visible = !show;
        if (!show) return;

        infoTitle.Text = b!.Def.Name;
        infoState.Text = b.StateText();

        switch (b.State)
        {
            case BuildState.Digging:
                infoProgress.Visible = true;
                infoProgress.Value = b.DigProgress * 100;
                break;
            case BuildState.Constructing:
                infoProgress.Visible = true;
                infoProgress.Value = b.BuildProgress * 100;
                break;
            default:
                infoProgress.Visible = false;
                break;
        }

        foreach (var r in Res.All)
        {
            var (chip, label) = infoChips[r];
            int i = (int)r;
            if (b.State != BuildState.Done)
            {
                chip.Visible = b.Cost(r) > 0;
                label.Text = $"{b.Delivered[i]}/{b.Cost(r)}";
            }
            else
            {
                chip.Visible = b.Def.IsStore || b.Def.Inputs.Contains(r) || b.Def.Outputs.Contains(r);
                label.Text = b.Def.IsStore ? b.Stock[i].ToString() : $"{b.Stock[i]}/{b.Def.MaxStock}";
            }
        }

        if (b.State != BuildState.Done) infoWorker.Text = $"Stavitelé na místě: {b.Builders.Count}";
        else if (b.Def.Worker is { } k)
            infoWorker.Text = b.Worker == null ? $"{Res.UnitName(k)}: chybí" : $"{Res.UnitName(k)}: {b.Worker.Status.ToLower()}";
        else infoWorker.Text = b.Def.Description;
    }
}

using Godot;

namespace km;

/// <summary>Hlavní menu nad živou vesnicí v pozadí: nová hra, nastavení, konec.</summary>
public partial class MainMenu : CanvasLayer
{
    Control mainButtons = null!, newGame = null!;
    SettingsPanel settings = null!;
    LineEdit seedEdit = null!;
    CheckBox revealCheck = null!;

    public override void _Ready()
    {
        Layer = 3;
        var root = new UiRoot();
        AddChild(root);

        // tmavší levá část, ať menu vynikne
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0, 0, 0, 0.6f));
        gradient.SetColor(1, new Color(0, 0, 0, 0f));
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = gradient, Width = 256, Height = 4, FillTo = new Vector2(1, 0) },
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        shade.OffsetRight = 720;

        var panel = new PanelContainer();
        root.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft, Control.LayoutPresetMode.Minsize);
        panel.GrowVertical = Control.GrowDirection.Both;
        panel.OffsetLeft = 70;
        panel.OffsetRight = 70;

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);

        vb.AddChild(MenuStyle.Heading("KM", 72));
        var sub = new Label { Text = "budovatelská strategie · prototyp", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 16);
        vb.AddChild(sub);
        vb.AddChild(new HSeparator());

        // hlavní volby
        var mb = new VBoxContainer();
        mb.AddThemeConstantOverride("separation", 10);
        mainButtons = mb;
        vb.AddChild(mb);
        var play = MenuStyle.BigButton("Nová hra");
        play.Pressed += () => Show(newGame);
        mb.AddChild(play);
        var set = MenuStyle.BigButton("Nastavení");
        set.Pressed += () => Show(settings);
        mb.AddChild(set);
        var quit = MenuStyle.BigButton("Ukončit");
        quit.Pressed += () => GetTree().Quit();
        mb.AddChild(quit);

        // nová hra
        var ng = new VBoxContainer();
        ng.AddThemeConstantOverride("separation", 10);
        newGame = ng;
        vb.AddChild(ng);
        ng.AddChild(MenuStyle.Section("Nová hra"));
        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 12);
        seedRow.AddChild(new Label { Text = "Semínko mapy", VerticalAlignment = VerticalAlignment.Center });
        seedEdit = new LineEdit { PlaceholderText = "náhodné", CustomMinimumSize = new Vector2(160, 34) };
        seedRow.AddChild(seedEdit);
        ng.AddChild(seedRow);
        revealCheck = new CheckBox { Text = "Odkrytá mapa (bez mlhy)", FocusMode = Control.FocusModeEnum.None };
        ng.AddChild(revealCheck);
        var start = MenuStyle.BigButton("Začít hru");
        start.Pressed += StartGame;
        ng.AddChild(start);
        var back = MenuStyle.BigButton("Zpět");
        back.Pressed += () => Show(mainButtons);
        ng.AddChild(back);

        settings = new SettingsPanel();
        settings.Back += () => Show(mainButtons);
        vb.AddChild(settings);

        var footer = new Label { Text = "prototyp · Godot 4.7", Modulate = new Color(1, 1, 1, 0.6f) };
        footer.AddThemeFontSizeOverride("font_size", 13);
        root.AddChild(footer);
        footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, 12);
        footer.GrowHorizontal = Control.GrowDirection.Begin;
        footer.GrowVertical = Control.GrowDirection.Begin;

        Show(mainButtons);
    }

    void Show(Control page)
    {
        mainButtons.Visible = page == mainButtons;
        newGame.Visible = page == newGame;
        settings.Visible = page == settings;
    }

    void StartGame()
    {
        int seed = seedEdit.Text.Trim().IsValidInt() ? seedEdit.Text.Trim().ToInt() : App.RandomSeed();
        App.Instance.StartGame(seed, revealCheck.ButtonPressed);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape } && !mainButtons.Visible)
        {
            Show(mainButtons);
            GetViewport().SetInputAsHandled();
        }
    }
}

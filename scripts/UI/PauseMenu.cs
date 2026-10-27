using Godot;

namespace km;

/// <summary>Herní menu na Esc: pozastaví hru (strom), nabízí nastavení a návrat do hlavního menu.</summary>
public partial class PauseMenu : CanvasLayer
{
    Control buttons = null!;
    SettingsPanel settings = null!;

    public override void _Ready()
    {
        Layer = 5;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        var root = new UiRoot();
        AddChild(root);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vb);

        var bb = new VBoxContainer();
        bb.AddThemeConstantOverride("separation", 10);
        buttons = bb;
        vb.AddChild(bb);
        bb.AddChild(MenuStyle.Heading("Pauza"));
        var resume = MenuStyle.BigButton("Pokračovat");
        resume.Pressed += Close;
        bb.AddChild(resume);
        var set = MenuStyle.BigButton("Nastavení");
        set.Pressed += () => ShowSettings(true);
        bb.AddChild(set);
        var menu = MenuStyle.BigButton("Hlavní menu");
        menu.Pressed += () => App.Instance.ShowMainMenu();
        bb.AddChild(menu);
        var quit = MenuStyle.BigButton("Ukončit hru");
        quit.Pressed += () => GetTree().Quit();
        bb.AddChild(quit);

        settings = new SettingsPanel();
        settings.Back += () => ShowSettings(false);
        vb.AddChild(settings);
        ShowSettings(false);
    }

    void ShowSettings(bool on)
    {
        buttons.Visible = !on;
        settings.Visible = on;
    }

    public void Open(bool showSettings = false)
    {
        ShowSettings(showSettings);
        Visible = true;
        GetTree().Paused = true;
    }

    public void Close()
    {
        Visible = false;
        GetTree().Paused = false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (settings.Visible) ShowSettings(false);
        else Close();
        GetViewport().SetInputAsHandled();
    }
}

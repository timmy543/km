using Godot;

namespace km;

/// <summary>Kořen rozhraní přes celé okno, který respektuje měřítko UI z nastavení.</summary>
public partial class UiRoot : Control
{
    public UiRoot()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiArt.Theme;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        float s = Settings.UiScale;
        var size = GetViewportRect().Size / s;
        if (Scale.X != s) Scale = new Vector2(s, s);
        if (Size != size) Size = size;
    }

    public override void _Ready() => _Process(0);
}

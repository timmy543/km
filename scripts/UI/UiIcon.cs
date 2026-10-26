using Godot;

namespace km;

public partial class UiIcon : Control
{
    public IconKind Kind;
    public BuildingDef? Def;

    public UiIcon() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => IconPainter.Draw(this, Kind, Def, new Rect2(Vector2.Zero, Size));
}

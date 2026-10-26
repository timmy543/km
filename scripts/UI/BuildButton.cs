using Godot;

namespace km;

/// <summary>Velké tlačítko s ikonou, popiskem, klávesovou zkratkou a cenou (dřevo/kámen).</summary>
public partial class BuildButton : Button
{
    public IconKind Kind;
    public BuildingDef? Def;
    public string Caption = "";
    public string Hotkey = "";

    public BuildButton()
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(92, 112);
    }

    public override void _Draw()
    {
        var font = UiArt.Serif;
        var bold = UiArt.SerifBold;
        float w = Size.X;
        float iconOffset = ButtonPressed ? 1f : 0f;

        IconPainter.Draw(this, Kind, Def, new Rect2(5, 6 + iconOffset, w - 10, 64));

        var captionPos = new Vector2(0, 87);
        DrawStringOutline(bold, captionPos, Caption, HorizontalAlignment.Center, w, 14, 4, new Color(0.08f, 0.05f, 0.02f));
        DrawString(bold, captionPos, Caption, HorizontalAlignment.Center, w, 14, UiArt.Parchment);

        if (Def != null)
        {
            // cena – malé ikonky s čísly
            float y = 101;
            float x = w / 2f - 30f;
            CostItem(font, new Vector2(x, y), IconKind.Wood, Def.CostWood);
            CostItem(font, new Vector2(x + 32f, y), IconKind.Stone, Def.CostStone);
        }

        if (Hotkey != "")
        {
            var badge = new Rect2(5, 5, 18, 17);
            DrawRect(badge, new Color(0.12f, 0.08f, 0.05f, 0.8f));
            DrawRect(badge, new Color(0.62f, 0.46f, 0.22f), false, 1f);
            DrawString(bold, new Vector2(5, 18), Hotkey, HorizontalAlignment.Center, 18, 12, UiArt.Gold);
        }
    }

    void CostItem(Font font, Vector2 p, IconKind kind, int count)
    {
        IconPainter.Draw(this, kind, null, new Rect2(p + new Vector2(0, -8), new Vector2(16, 16)));
        var tp = p + new Vector2(17, 5);
        DrawStringOutline(font, tp, count.ToString(), HorizontalAlignment.Left, -1, 13, 3, new Color(0.08f, 0.05f, 0.02f));
        DrawString(font, tp, count.ToString(), HorizontalAlignment.Left, -1, 13, UiArt.Parchment);
    }
}

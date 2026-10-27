using Godot;

namespace km;

/// <summary>Společné prvky menu: nadpisy a velká tlačítka.</summary>
public static class MenuStyle
{
    public static Label Heading(string text, int size = 30)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontOverride("font", UiArt.SerifBold);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", UiArt.Gold);
        l.AddThemeConstantOverride("outline_size", 5);
        return l;
    }

    public static Label Section(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", UiArt.SerifBold);
        l.AddThemeFontSizeOverride("font_size", 18);
        l.AddThemeColorOverride("font_color", UiArt.Gold);
        return l;
    }

    public static Button BigButton(string text)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(320, 50) };
        b.AddThemeFontOverride("font", UiArt.SerifBold);
        b.AddThemeFontSizeOverride("font_size", 21);
        return b;
    }
}

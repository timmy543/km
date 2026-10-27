using System.Linq;
using Godot;

namespace km;

/// <summary>
/// Ladicí přepínače hry z příkazové řádky (za `--`):
///   --demo              automaticky naplánuje dřevorubce, pilu, kamenolom a silnice
///   --timescale=4       zrychlení simulace
///   --zoom=1.0          počáteční přiblížení
///   --paths, --select   zobrazit cesty / vybrat poslední budovu
///   --pause, --settings otevřít herní menu / rovnou nastavení
/// </summary>
public partial class DevTools : Node
{
    bool demo, select, pause, settings, done;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--demo") demo = true;
            else if (a == "--paths") Main.Instance.ShowPaths = true;
            else if (a == "--select") select = true;
            else if (a == "--pause") pause = true;
            else if (a == "--settings") pause = settings = true;
            else if (a.StartsWith("--timescale=")) Engine.TimeScale = a["--timescale=".Length..].ToFloat();
            else if (a.StartsWith("--zoom=")) Main.Instance.Cam.ApplyZoom(a["--zoom=".Length..].ToFloat());
        }
    }

    public override void _Process(double delta)
    {
        if (done) return;
        done = true;
        if (demo) Main.Instance.AutoBuild();
        if (select) Main.Instance.Selected = Main.Instance.Eco.Buildings.LastOrDefault();
        if (pause) Main.Instance.OpenPauseMenu(settings);
    }
}

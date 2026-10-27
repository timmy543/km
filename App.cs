using System.Linq;
using Godot;

namespace km;

/// <summary>
/// Kořen aplikace (hlavní scéna): načte nastavení, přepíná mezi hlavním menu a hrou.
/// Ladicí přepínače z příkazové řádky (za `--`):
///   --seed=123 / --demo / --play   rovnou spustí hru (bez menu)
///   --reveal                       odkrytá mapa
///   --screenshot=a.png             po --shot-after sekundách uloží snímek a ukončí aplikaci
/// Další přepínače hry zpracovává DevTools.
/// </summary>
public partial class App : Node
{
    public static App Instance { get; private set; } = null!;

    Node? game, menu;
    string? shotPath;
    double shotAfter = 3;
    ulong startMs;

    public static int RandomSeed() => (int)(GD.Randi() % 100000);

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Settings.Load();
        Settings.Apply(GetTree().Root);
        UiArt.Init();

        var args = OS.GetCmdlineUserArgs();
        int seed = RandomSeed();
        bool direct = false, reveal = false;
        foreach (var a in args)
        {
            if (a.StartsWith("--seed="))
            {
                seed = a["--seed=".Length..].ToInt();
                direct = true;
            }
            else if (a is "--demo" or "--play") direct = true;
            else if (a == "--reveal") reveal = true;
            else if (a.StartsWith("--screenshot=")) shotPath = a["--screenshot=".Length..];
            else if (a.StartsWith("--shot-after=")) shotAfter = a["--shot-after=".Length..].ToFloat();
            else if (a.StartsWith("--make-icon="))
            {
                MakeIcon(a["--make-icon=".Length..]);
                return;
            }
        }
        startMs = Time.GetTicksMsec();

        if (direct) DoStartGame(seed, reveal);
        else DoShowMainMenu();
    }

    public void StartGame(int seed, bool reveal) => Callable.From(() => DoStartGame(seed, reveal)).CallDeferred();

    public void ShowMainMenu() => Callable.From(DoShowMainMenu).CallDeferred();

    void DoStartGame(int seed, bool reveal)
    {
        Clear();
        Engine.TimeScale = 1;
        game = new Main { Seed = seed, RevealAll = reveal };
        AddChild(game);
    }

    void DoShowMainMenu()
    {
        Clear();
        Engine.TimeScale = 1;
        game = new Main { Seed = RandomSeed(), MenuBackground = true };
        AddChild(game);
        menu = new MainMenu();
        AddChild(menu);
    }

    /// <summary>Starou hru hned odpojí ze stromu, aby už nic nepočítala se sdíleným Main.Instance.</summary>
    void Clear()
    {
        GetTree().Paused = false;
        foreach (var n in new[] { menu, game }.Where(n => n != null))
        {
            RemoveChild(n);
            n!.QueueFree();
        }
        game = menu = null;
    }

    /// <summary>Vykreslí ikonu aplikace (sklad na dřevěném štítku) do PNG a ukončí se.</summary>
    async void MakeIcon(string path)
    {
        var vp = new SubViewport { Size = new Vector2I(256, 256), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        var icon = new IconCanvas();
        vp.AddChild(icon);
        AddChild(vp);
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = vp.GetTexture().GetImage();
        img.SavePng(path);
        SaveIco(img, path.GetBaseName() + ".ico");
        GD.Print($"km: ikona uložena do {path} (+ .ico)");
        GetTree().Quit();
    }

    /// <summary>ICO se všemi běžnými velikostmi (každá jako PNG uvnitř), jak ho chce export pro Windows.</summary>
    static void SaveIco(Image source, string path)
    {
        int[] sizes = { 16, 32, 48, 64, 128, 256 };
        var pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            var img = (Image)source.Duplicate();
            img.Resize(sizes[i], sizes[i], Image.Interpolation.Lanczos);
            pngs[i] = img.SavePngToBuffer();
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.Store16(0);
        file.Store16(1);
        file.Store16((ushort)sizes.Length);
        uint offset = 6 + 16u * (uint)sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            file.Store8((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            file.Store8((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            file.Store8(0);
            file.Store8(0);
            file.Store16(1);
            file.Store16(32);
            file.Store32((uint)pngs[i].Length);
            file.Store32(offset);
            offset += (uint)pngs[i].Length;
        }
        foreach (var p in pngs) file.StoreBuffer(p);
    }

    public override void _Process(double delta)
    {
        if (shotPath == null || (Time.GetTicksMsec() - startMs) / 1000.0 < shotAfter) return;
        GetViewport().GetTexture().GetImage().SavePng(shotPath);
        GD.Print($"km: snímek uložen do {shotPath}");
        shotPath = null;
        GetTree().Quit();
    }
}

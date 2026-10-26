using System;
using System.Collections.Generic;
using Godot;

namespace km;

public abstract partial class Unit : Node2D
{
    public UnitKind Kind;
    public Vector2 GridPos;
    public ResourceType? Carrying;
    public Pose Pose;
    public bool FacingLeft;
    public float BaseSpeed = 1.6f; // dlaždic za sekundu na trávě

    protected readonly Queue<UnitAction> Actions = new();
    UnitAction? current;
    protected float AnimTime;
    Vector2 lane;   // drobný posun, aby se lidé na cestě nepřekrývali
    bool inside;

    public Vector2I Tile => new(Mathf.FloorToInt(GridPos.X), Mathf.FloorToInt(GridPos.Y));
    public WalkAction? ActiveWalk => current as WalkAction;

    public bool Inside
    {
        get => inside;
        set
        {
            inside = value;
            Visible = !value;
        }
    }

    protected static GameMap Map => Main.Instance.Map;
    protected static Economy Eco => Main.Instance.Eco;

    public override void _Ready()
    {
        lane = new Vector2((float)GD.RandRange(-7.0, 7.0), (float)GD.RandRange(-2.5, 2.5));
        AnimTime = GD.Randf() * 10f;
        SyncPosition();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (current == null && Actions.Count == 0) Think();
        if (current == null && Actions.Count > 0)
        {
            current = Actions.Dequeue();
            current.Start(this);
        }

        if (current != null)
        {
            var status = current.Update(this, dt);
            if (status != ActionStatus.Running)
            {
                current = null;
                if (status == ActionStatus.Failed)
                {
                    Actions.Clear();
                    OnFailed();
                }
            }
        }
        else Pose = Pose.Idle;

        AnimTime += dt;
        SyncPosition();
        if (Visible) QueueRedraw();
    }

    /// <summary>Zavolá se, když jednotka nemá co dělat. Má si naplánovat další akce.</summary>
    protected virtual void Think() { }

    /// <summary>Některá akce selhala (cesta neexistuje, cíl zmizel…). Uklidit rezervace.</summary>
    protected virtual void OnFailed() { }

    public void ClearActions()
    {
        Actions.Clear();
        current = null;
    }

    protected void Walk(params Vector2I[] targets) => Actions.Enqueue(new WalkAction(targets));
    protected void Wait(float seconds, Pose pose = Pose.Idle, Vector2? face = null) => Actions.Enqueue(new WaitAction(seconds, pose, face));
    protected void Do(Func<bool> fn) => Actions.Enqueue(new CallAction(fn));

    public void FaceTowards(Vector2 grid)
    {
        var d = grid - GridPos;
        float sx = d.X - d.Y; // směr na obrazovce
        if (Mathf.Abs(sx) > 0.01f) FacingLeft = sx < 0;
    }

    public void SyncPosition() => Position = Map.GroundToScreen(GridPos) + lane;

    // ---------------------------------------------------------------- vykreslení

    static readonly Color Skin = new(0.93f, 0.74f, 0.58f);
    static readonly Color Legs = new(0.30f, 0.22f, 0.15f);
    static readonly Color Outline = new(0.12f, 0.08f, 0.05f, 0.85f);

    protected virtual Color TunicColor => new(0.75f, 0.62f, 0.38f);
    protected virtual Color HatColor => new(0.45f, 0.32f, 0.18f);

    public override void _Draw()
    {
        float t = AnimTime;
        bool walking = Pose == Pose.Walk;
        float bob = walking ? Mathf.Abs(Mathf.Sin(t * 9f)) * 1.5f : 0f;
        float swing = walking ? Mathf.Sin(t * 9f) * 3.5f : 0f;
        float sx = FacingLeft ? -1f : 1f;

        Gfx.Ellipse(this, new Vector2(0, 0), 8f, 3.2f, new Color(0, 0, 0, 0.28f));

        // nohy
        DrawLine(new Vector2(-2.2f, -9f - bob), new Vector2(-2.2f + swing, 0), Legs, 2.6f);
        DrawLine(new Vector2(2.2f, -9f - bob), new Vector2(2.2f - swing, 0), Legs, 2.6f);

        // tělo
        float bot = -8f - bob, top = -21f - bob;
        var body = new[] { new Vector2(-5.5f, bot), new Vector2(5.5f, bot), new Vector2(4.2f, top), new Vector2(-4.2f, top) };
        DrawColoredPolygon(body, TunicColor);
        DrawColoredPolygon(new[] { body[1], body[2], new Vector2(1.5f, top), new Vector2(1.5f, bot) }, Gfx.Shade(TunicColor, 0.8f));
        DrawPolyline(new[] { body[0], body[1], body[2], body[3], body[0] }, Outline, 1f);
        DrawLine(new Vector2(-5.2f, bot - 3f), new Vector2(5.2f, bot - 3f), Gfx.Shade(TunicColor, 0.55f), 1.5f);

        // hlava
        var head = new Vector2(0, top - 4.2f);
        DrawCircle(head, 4.4f, Outline);
        DrawCircle(head, 3.7f, Skin);
        DrawHat(head, sx);

        // ruce / nástroj
        var shoulder = new Vector2(sx * 4.2f, top + 2.5f);
        if (Pose == Pose.Work) DrawWork(shoulder, sx, t);
        else
        {
            float arm = walking ? -swing * 0.6f : 0f;
            DrawLine(shoulder, shoulder + new Vector2(sx * 1.5f + arm, 8f), Gfx.Shade(TunicColor, 0.75f), 2.2f);
        }

        if (Carrying is { } r) DrawCarried(r, new Vector2(0, top - 11f));
    }

    protected virtual void DrawHat(Vector2 head, float sx)
    {
        DrawColoredPolygon(new[] { head + new Vector2(-4.5f, -1.5f), head + new Vector2(4.5f, -1.5f), head + new Vector2(2.5f, -5f), head + new Vector2(-2.5f, -5f) }, HatColor);
    }

    protected virtual void DrawWork(Vector2 shoulder, float sx, float t)
    {
        // obecné máchání nástrojem
        float a = -1.9f + (Mathf.Sin(t * 7f) * 0.5f + 0.5f) * 1.9f;
        var dir = new Vector2(Mathf.Cos(a) * sx, Mathf.Sin(a));
        var hand = shoulder + dir * 5f;
        DrawLine(shoulder, hand, Gfx.Shade(TunicColor, 0.75f), 2.2f);
        DrawLine(hand - dir * 2f, hand + dir * 9f, new Color(0.45f, 0.30f, 0.15f), 1.6f);
        var tip = hand + dir * 9f;
        var perp = new Vector2(-dir.Y, dir.X);
        DrawLine(tip - perp * 3f, tip + perp * 3f, new Color(0.55f, 0.56f, 0.6f), 2.6f);
    }

    void DrawCarried(ResourceType r, Vector2 p)
    {
        switch (r)
        {
            case ResourceType.Trunk:
                DrawLine(p + new Vector2(-8, 1), p + new Vector2(8, -1), Outline, 6f);
                DrawLine(p + new Vector2(-7.5f, 1), p + new Vector2(7.5f, -1), Res.Tint(r), 4.5f);
                DrawCircle(p + new Vector2(8, -1), 2.4f, new Color(0.80f, 0.64f, 0.40f));
                break;
            case ResourceType.Wood:
                DrawRect(new Rect2(p + new Vector2(-8, -3), new Vector2(16, 6)), Outline);
                DrawRect(new Rect2(p + new Vector2(-7.5f, -2.5f), new Vector2(15, 2.2f)), Res.Tint(r));
                DrawRect(new Rect2(p + new Vector2(-7.5f, 0.3f), new Vector2(15, 2.2f)), Gfx.Shade(Res.Tint(r), 0.85f));
                break;
            default:
                DrawCircle(p, 5f, Outline);
                DrawCircle(p, 4.2f, Res.Tint(r));
                DrawCircle(p + new Vector2(-1.4f, -1.4f), 1.6f, new Color(0.8f, 0.8f, 0.78f));
                break;
        }
    }
}

using System;
using System.Collections.Generic;
using Godot;

namespace km;

public enum ActionStatus { Running, Done, Failed }

public enum Pose { Idle, Walk, Work }

/// <summary>Jeden krok chování jednotky. Jednotka si je řadí do fronty a plní postupně.</summary>
public abstract class UnitAction
{
    public virtual void Start(Unit u) { }
    public abstract ActionStatus Update(Unit u, float dt);
}

public sealed class WaitAction : UnitAction
{
    float left;
    readonly Pose pose;
    readonly Vector2? face;

    public WaitAction(float seconds, Pose pose, Vector2? faceGrid)
    {
        left = seconds;
        this.pose = pose;
        face = faceGrid;
    }

    public override void Start(Unit u)
    {
        if (face is { } f) u.FaceTowards(f);
    }

    public override ActionStatus Update(Unit u, float dt)
    {
        u.Pose = pose;
        left -= dt;
        return left <= 0 ? ActionStatus.Done : ActionStatus.Running;
    }
}

public sealed class CallAction : UnitAction
{
    readonly Func<bool> fn;
    public CallAction(Func<bool> fn) => this.fn = fn;
    public override ActionStatus Update(Unit u, float dt) => fn() ? ActionStatus.Done : ActionStatus.Failed;
}

/// <summary>
/// Chůze po cestě z A*. Když se cesta za pochodu zablokuje (nová budova), přepočítá se.
/// Jednotky se navzájem neblokují – žádné zácpy jako v originále.
/// </summary>
public sealed class WalkAction : UnitAction
{
    readonly Vector2I[] targets;
    List<Vector2I>? path;
    int idx;
    int replans;

    public IReadOnlyList<Vector2I>? Path => path;
    public int Index => idx;

    public WalkAction(Vector2I[] targets) => this.targets = targets;

    public override void Start(Unit u) => Plan(u);

    void Plan(Unit u)
    {
        path = Main.Instance.Map.FindPathToAny(u.Tile, targets);
        idx = path != null && path.Count > 1 && path[0] == u.Tile ? 1 : 0;
    }

    public override ActionStatus Update(Unit u, float dt)
    {
        if (path == null) return ActionStatus.Failed;
        if (idx >= path.Count) return ActionStatus.Done;

        var map = Main.Instance.Map;
        var next = path[idx];
        if (!map.IsWalkable(next))
        {
            if (++replans > 20) return ActionStatus.Failed;
            Plan(u);
            return path == null ? ActionStatus.Failed : ActionStatus.Running;
        }

        u.Pose = Pose.Walk;
        bool last = idx == path.Count - 1;
        var goal = Iso.TileCenter(next);
        var to = goal - u.GridPos;
        float dist = to.Length();
        float step = u.BaseSpeed * map.SpeedFactor(u.Tile) * dt;
        if (dist > 0.001f) u.FaceTowards(goal);

        if (dist <= step)
        {
            u.GridPos = goal;
            idx++;
        }
        else
        {
            u.GridPos += to / dist * step;
            // mírné zaoblení zatáček – na mezilehlý bod se nemusí dojít přesně
            if (!last && dist - step < 0.25f) idx++;
        }
        return idx >= path.Count ? ActionStatus.Done : ActionStatus.Running;
    }
}

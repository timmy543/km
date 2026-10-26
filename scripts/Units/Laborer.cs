using Godot;

namespace km;

/// <summary>Stavitel – staví silnice a budovy (nejdřív srovná terén, pak staví z dovezeného materiálu).</summary>
public partial class Laborer : Unit
{
    public const float BuildStepTime = 1.6f;

    public IWorkOrder? Order;

    public Laborer() => Kind = UnitKind.Laborer;

    protected override Color TunicColor => new(0.58f, 0.36f, 0.20f);
    protected override Color HatColor => new(0.85f, 0.70f, 0.30f);

    public void AssignOrder(IWorkOrder o)
    {
        ClearActions();
        Order = o;
        o.Builders.Add(this);
    }

    public void ReleaseOrder()
    {
        Order?.Builders.Remove(this);
        Order = null;
    }

    protected override void Think()
    {
        if (Order == null) return;
        if (Order.IsFinished)
        {
            ReleaseOrder();
            return;
        }

        switch (Order)
        {
            case RoadPlan rp:
                if (Tile != rp.Tile)
                {
                    Walk(rp.Tile);
                    return;
                }
                Wait(2.5f, Pose.Work);
                Do(() =>
                {
                    if (!rp.IsFinished) Main.Instance.CompleteRoad(rp);
                    ReleaseOrder();
                    return true;
                });
                break;

            case Building b:
                if (Tile != b.Entrance)
                {
                    Walk(b.Entrance);
                    return;
                }
                var center = new Vector2(b.Origin.X + b.Def.Size.X / 2f, b.Origin.Y + b.Def.Size.Y / 2f);
                if (b.State == BuildState.Digging)
                {
                    Wait(1f, Pose.Work, center);
                    Do(() =>
                    {
                        if (!b.Removed) b.AddDig(1f);
                        return true;
                    });
                }
                else if (b.TryReserveStep())
                {
                    Wait(BuildStepTime, Pose.Work, center);
                    Do(() =>
                    {
                        if (!b.Removed) b.CompleteStep();
                        return true;
                    });
                }
                else Wait(0.5f, Pose.Idle, center); // čeká na materiál
                break;
        }
    }

    protected override void OnFailed()
    {
        // k zakázce se nedá dojít – chvíli ji nikomu nepřidělovat
        if (Order != null) Order.RetryAt = Time.GetTicksMsec() / 1000.0 + 5.0;
        ReleaseOrder();
        Wait(1f);
    }

    protected override void DrawWork(Vector2 shoulder, float sx, float t)
    {
        // kladivo
        float a = -1.6f + (Mathf.Sin(t * 10f) * 0.5f + 0.5f) * 1.5f;
        var dir = new Vector2(Mathf.Cos(a) * sx, Mathf.Sin(a));
        var hand = shoulder + dir * 5f;
        DrawLine(shoulder, hand, Gfx.Shade(TunicColor, 0.75f), 2.2f);
        DrawLine(hand, hand + dir * 7f, new Color(0.45f, 0.30f, 0.15f), 1.6f);
        var tip = hand + dir * 7f;
        var perp = new Vector2(-dir.Y, dir.X);
        DrawLine(tip - perp * 2.5f, tip + perp * 2.5f, new Color(0.35f, 0.35f, 0.38f), 3.2f);
    }
}

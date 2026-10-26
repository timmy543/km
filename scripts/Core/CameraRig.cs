using Godot;

namespace km;

/// <summary>Kamera: WASD/šipky, kolečko (zoom ke kurzoru), tažení prostředním tlačítkem.</summary>
public partial class CameraRig : Camera2D
{
    const float PanSpeed = 900f;
    const float MinZoom = 0.35f, MaxZoom = 3f;

    public Rect2 Bounds;
    bool dragging;
    ulong lastTicks;

    public override void _Ready()
    {
        Zoom = new Vector2(1.2f, 1.2f);
        lastTicks = Time.GetTicksUsec();
    }

    public override void _Process(double delta)
    {
        // vlastní měření času, aby kamera fungovala i při pauze (TimeScale = 0)
        ulong now = Time.GetTicksUsec();
        float dt = Mathf.Min((now - lastTicks) / 1_000_000f, 0.1f);
        lastTicks = now;

        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;
        if (dir != Vector2.Zero)
        {
            Position += dir.Normalized() * PanSpeed * dt / Zoom.X;
            ClampToBounds();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } mb:
                ZoomAt(1.15f, mb.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } mb:
                ZoomAt(1f / 1.15f, mb.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } mb:
                dragging = mb.Pressed;
                break;
            case InputEventMouseMotion mm when dragging:
                Position -= mm.Relative / Zoom.X;
                ClampToBounds();
                break;
        }
    }

    public void ApplyZoom(float z)
    {
        z = Mathf.Clamp(z, MinZoom, MaxZoom);
        Zoom = new Vector2(z, z);
    }

    void ZoomAt(float factor, Vector2 screenPos)
    {
        float old = Zoom.X;
        float z = Mathf.Clamp(old * factor, MinZoom, MaxZoom);
        var offset = screenPos - GetViewportRect().Size / 2f;
        var world = Position + offset / old;
        Zoom = new Vector2(z, z);
        Position = world - offset / z;
        ClampToBounds();
    }

    public void JumpTo(Vector2 world)
    {
        Position = world;
        ClampToBounds();
    }

    void ClampToBounds()
    {
        Position = new Vector2(
            Mathf.Clamp(Position.X, Bounds.Position.X, Bounds.End.X),
            Mathf.Clamp(Position.Y, Bounds.Position.Y, Bounds.End.Y));
    }
}

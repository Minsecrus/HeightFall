using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>左手移动摇杆。每次只捕获自己的手指，右手施法可以同时进行。</summary>
public partial class TouchJoystick : Control
{
    public const float JoystickSize = 240f;
    const float Radius = 96f;
    const float Travel = 66f;
    const float DeadZone = 0.14f;
    static readonly Vector2 Center = new(JoystickSize / 2, JoystickSize / 2);
    int _finger = -1;
    Vector2 _stick;
    Unit? _gestureHero;

    public PlayerController Player { get; set; } = null!;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        if (_finger >= 0 && !GestureHeroIsCurrent())
            CancelGesture();
    }

    public override void _Input(InputEvent @event)
    {
        // Tab / 切换按钮可以在同一批输入中先于旧手指的拖动或松手发生。
        if (_finger >= 0 && !GestureHeroIsCurrent())
            CancelGesture();
        if (!Visible)
            return;
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Index == _finger)
            {
                if (!touch.Pressed || touch.Canceled)
                    CancelGesture();
                GetViewport().SetInputAsHandled();
            }
            else if (touch.Pressed && !touch.Canceled && _finger < 0)
            {
                if (!Player.Hero.IsAlive) return;
                var point = GetGlobalTransformWithCanvas().AffineInverse() * touch.Position;
                if (point.DistanceTo(Center) > Radius + 20)
                    return;
                _finger = touch.Index;
                _gestureHero = Player.Hero;
                UpdateStick(point);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventScreenDrag drag && drag.Index == _finger)
        {
            UpdateStick(GetGlobalTransformWithCanvas().AffineInverse() * drag.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    public void CancelGesture()
    {
        var stopMovement = _finger >= 0 && Player is not null && Player.Hero == _gestureHero;
        _finger = -1;
        _stick = Vector2.Zero;
        _gestureHero = null;
        // 切换英雄已经由 Player 取消旧移动；迟来的旧事件不能停止新英雄的新手势。
        if (stopMovement)
            Player!.SetTouchMove(Vector2.Zero);
        QueueRedraw();
    }

    bool GestureHeroIsCurrent() => _gestureHero is { IsAlive: true } && _gestureHero == Player.Hero;

    void UpdateStick(Vector2 point)
    {
        _stick = ((point - Center) / Travel).LimitLength();
        var amount = _stick.Length();
        Player.SetTouchMove(amount <= DeadZone ? Vector2.Zero :
            _stick.Normalized() * ((amount - DeadZone) / (1f - DeadZone)));
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if ((long)what is NotificationApplicationFocusOut or NotificationWMWindowFocusOut or NotificationApplicationPaused)
            CancelGesture();
    }

    public override void _Draw()
    {
        var border = new Color("#C9D0D6") { A = 0.8f };
        DrawCircle(Center, Radius, new Color(1, 1, 1, 0.34f));
        DrawArc(Center, Radius, 0, Mathf.Tau, 64, border, 2f, true);
        DrawArc(Center, Travel, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.55f), 1.5f, true);
        foreach (var direction in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
        {
            var tip = Center + direction * (Radius - 13);
            var side = direction.Orthogonal() * 5;
            DrawPolyline([tip - direction * 6 + side, tip, tip - direction * 6 - side], border, 2f, true);
        }
        var knob = Center + _stick * Travel;
        DrawCircle(knob + new Vector2(0, 3), 33, new Color(0.1f, 0.2f, 0.3f, 0.1f));
        DrawCircle(knob, 33, new Color(1, 1, 1, _finger >= 0 ? 0.94f : 0.76f));
        DrawArc(knob, 33, 0, Mathf.Tau, 40, _finger >= 0 ? Palette.Ally : border, 2f, true);
        DrawCircle(knob, 8, new Color(Palette.Ally, _finger >= 0 ? 0.7f : 0.4f));
    }
}

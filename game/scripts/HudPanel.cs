using System;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>左上角金币入口；两端都点击这里打开同一个 Godot 商店。</summary>
public partial class HudPanel : Control
{
    public const float PanelWidth = 170f;
    public const float PanelHeight = 64f;
    public const float TouchWidth = 170f;
    public const float TouchHeight = 84f;

    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;
    public bool TouchMode { get; set; }
    public event Action? ShopRequested;
    int _finger = -1;
    Vector2 _press;
    long _cancelSerial;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;
    public override void _Process(double delta) => QueueRedraw();

    public override void _GuiInput(InputEvent @event)
    {
        if (TouchMode && @event is InputEventMouse { Device: -1 })
        {
            AcceptEvent();
            return;
        }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            ShopRequested?.Invoke();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!TouchMode || !IsVisibleInTree())
            return;
        if (@event is InputEventScreenTouch touch)
        {
            var local = GetGlobalTransformWithCanvas().AffineInverse() * touch.Position;
            if (touch.Index == _finger)
            {
                if (!touch.Pressed || touch.Canceled)
                {
                    _finger = -1;
                    if (!touch.Canceled && TouchPlatform.ReadCancelSerial() == _cancelSerial
                        && new Rect2(Vector2.Zero, Size).HasPoint(local) && local.DistanceTo(_press) < 24)
                        ShopRequested?.Invoke();
                }
                GetViewport().SetInputAsHandled();
            }
            else if (touch.Pressed && !touch.Canceled && _finger < 0 && new Rect2(Vector2.Zero, Size).HasPoint(local))
            {
                _finger = touch.Index;
                _press = local;
                _cancelSerial = TouchPlatform.ReadCancelSerial();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventScreenDrag drag && drag.Index == _finger)
            GetViewport().SetInputAsHandled();
    }

    public void CancelGesture() => _finger = -1;

    public override void _Notification(int what)
    {
        if ((long)what is NotificationWMWindowFocusOut or NotificationApplicationFocusOut or NotificationApplicationPaused)
            CancelGesture();
    }

    public override void _Draw()
    {
        var center = new Vector2(29, Size.Y / 2);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1, 1, 1, _finger >= 0 ? 0.94f : 0.7f));
        DrawCircle(center, 13, Palette.Gold);
        DrawArc(center, 9, 0, Mathf.Tau, 32, new Color(1, 1, 1, 0.65f), 1.5f, true);
        DrawLine(center + new Vector2(0, -6), center + new Vector2(0, 6), Colors.White, 2, true);
        DrawString(ThemeDB.FallbackFont, new Vector2(54, Size.Y / 2 + 11), $"{Player.Hero.Gold}",
            fontSize: TouchMode ? 34 : 28, modulate: Palette.Neutral);
    }
}

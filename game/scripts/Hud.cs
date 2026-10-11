using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>共享界面层：桌面点击操作，触屏左手移动、右手攻击和拖动施法。</summary>
public partial class Hud : CanvasLayer
{
    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;
    public CameraRig CameraRig { get; set; } = null!;
    public UnitView Units { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;
    public bool TouchMode { get; private set; }
    HudPanel _panel = null!;
    SkillBar _skills = null!;
    TouchJoystick _joystick = null!;
    TopBar _top = null!;
    ShopPanel _shop = null!;
    Vector2I _desktopScale;
    long _cancelSerial;
    Unit? _gestureHero;
    bool _heroAlive;

    public override void _Ready()
    {
        _desktopScale = GetWindow().ContentScaleSize;
        AddChild(_top = new TopBar { World = World, Player = Player, Viewer = Viewer });
        AddChild(_panel = new HudPanel { World = World, Player = Player });
        AddChild(_skills = new SkillBar { World = World, Player = Player });
        AddChild(_joystick = new TouchJoystick { Player = Player, Visible = false });
        AddChild(_shop = new ShopPanel { World = World, Player = Player, Visible = false });
        _panel.ShopRequested += OpenShop;
        _shop.Closed += CloseShop;
        Player.HeroChanged += OnHeroChanged;
        GetViewport().SizeChanged += Layout;
        var mobile = OS.HasFeature("android") || OS.HasFeature("ios") || DisplayServer.IsTouchscreenAvailable();
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg == "--touch")
                mobile = true;
        SetTouchMode(mobile);
        Layout();
        _cancelSerial = TouchPlatform.ReadCancelSerial();
        _gestureHero = Player.Hero;
        _heroAlive = Player.Hero.IsAlive;
    }

    public override void _ExitTree()
    {
        Player.HeroChanged -= OnHeroChanged;
        GetViewport().SizeChanged -= Layout;
    }

    void OnHeroChanged()
    {
        CancelGestures();
        _gestureHero = Player.Hero;
        _heroAlive = Player.Hero.IsAlive;
    }

    public override void _Process(double delta)
    {
        if (!TouchMode)
            return;
        if (_gestureHero != Player.Hero || _heroAlive != Player.Hero.IsAlive)
        {
            _gestureHero = Player.Hero;
            _heroAlive = Player.Hero.IsAlive;
            CancelGestures();
        }
        var serial = TouchPlatform.ReadCancelSerial();
        if (serial != _cancelSerial)
        {
            _cancelSerial = serial;
            CancelGestures();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // A browser may expose touch only after its first actual touch event.
        if (!TouchMode && @event is InputEventScreenTouch { Pressed: true, Canceled: false })
            SetTouchMode(true);
    }

    public void SetTouchMode(bool enabled)
    {
        if (TouchMode == enabled)
            return;
        CancelGestures();
        TouchMode = enabled;
        Player.TouchMode = enabled;
        CameraRig.TouchMode = enabled;
        Units.TouchMode = enabled;
        _skills.TouchMode = enabled;
        _panel.TouchMode = enabled;
        _top.TouchMode = enabled;
        _shop.TouchMode = enabled;
        _joystick.Visible = enabled && !_shop.IsOpen;
        // Smaller UI coordinates keep the controls large enough for thumbs on a phone.
        GetWindow().ContentScaleSize = enabled ? new Vector2I(960, 540) : _desktopScale;
        Layout();
    }

    void Layout()
    {
        if (_skills is null)
            return;
        CancelGestures();
        var viewport = GetViewport().GetVisibleRect().Size;
        if (!TouchMode)
        {
            _panel.Scale = _skills.Scale = Vector2.One;
            Place(_panel, Control.LayoutPreset.TopLeft,
                16, 16, HudPanel.PanelWidth + 16, HudPanel.PanelHeight + 16);
            Place(_skills, Control.LayoutPreset.BottomRight, -SkillBar.BarWidth - 12, -SkillBar.BarHeight - 8, -12, -8);
            _shop.SetLayout(new Rect2(16, 16, viewport.X - 32, viewport.Y - 32));
            return;
        }

        var margins = SafeMargins(viewport);
        var portrait = viewport.Y > viewport.X;
        var skillScale = portrait ? 1.25f : 1f;
        // Keep a gap between the two hands even in a narrow portrait viewport.
        skillScale = Mathf.Min(skillScale, (viewport.X - margins.X - margins.Z - TouchJoystick.JoystickSize - 24) / SkillBar.TouchWidth);
        _skills.Scale = Vector2.One * Mathf.Max(0.75f, skillScale);
        _skills.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _skills.Size = new Vector2(SkillBar.TouchWidth, SkillBar.TouchHeight);
        _skills.Position = new Vector2(viewport.X - margins.Z - _skills.Size.X * _skills.Scale.X,
            viewport.Y - margins.W - _skills.Size.Y * _skills.Scale.Y);
        _joystick.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _joystick.Size = Vector2.One * TouchJoystick.JoystickSize;
        _joystick.Position = new Vector2(margins.X, viewport.Y - margins.W - TouchJoystick.JoystickSize);
        _panel.Scale = Vector2.One * (portrait ? 1.25f : 1f);
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _panel.Size = new Vector2(HudPanel.TouchWidth, HudPanel.TouchHeight);
        _panel.Position = new Vector2(margins.X, margins.Y);
        _top.SafeInsets = margins;
        _shop.SetLayout(new Rect2(margins.X, margins.Y, viewport.X - margins.X - margins.Z,
            viewport.Y - margins.Y - margins.W));
    }

    Vector4 SafeMargins(Vector2 viewport)
    {
        const float padding = 16;
        var margins = new Vector4(padding, padding, padding, padding);
        if (OS.HasFeature("web"))
            return margins + TouchPlatform.ReadWebSafeInsets(viewport);
        // Android/iOS expose physical screen cutout coordinates.
        if (!OS.HasFeature("android") && !OS.HasFeature("ios"))
            return margins;
        var safe = DisplayServer.GetDisplaySafeArea();
        var pixels = GetWindow().Size;
        if (safe.Size.X <= 0 || safe.Size.Y <= 0 || pixels.X <= 0 || pixels.Y <= 0)
            return margins;
        var ratio = viewport / new Vector2(pixels.X, pixels.Y);
        return new Vector4(padding + Mathf.Max(0, safe.Position.X) * ratio.X,
            padding + Mathf.Max(0, safe.Position.Y) * ratio.Y,
            padding + Mathf.Max(0, pixels.X - safe.End.X) * ratio.X,
            padding + Mathf.Max(0, pixels.Y - safe.End.Y) * ratio.Y);
    }

    public override void _Notification(int what)
    {
        if (_joystick is not null && (long)what is NotificationWMWindowFocusOut or NotificationApplicationFocusOut or NotificationApplicationPaused)
        {
            CancelGestures();
        }
    }

    void CancelGestures()
    {
        _joystick.CancelGesture();
        _skills.CancelGestures();
        _panel.CancelGesture();
        if (TouchMode || Player.TouchMode)
            Player.CancelTouchInput();
    }

    void OpenShop()
    {
        CancelGestures();
        Player.CancelTouchInput();
        _joystick.Visible = _skills.Visible = false;
        CameraRig.UiBlocked = true;
        Player.SetProcess(false);
        Player.SetProcessUnhandledInput(false);
        _shop.Open();
    }

    void CloseShop()
    {
        _joystick.Visible = TouchMode;
        _skills.Visible = true;
        CameraRig.UiBlocked = false;
        Player.SetProcess(true);
        Player.SetProcessUnhandledInput(true);
    }

    static void Place(Control control, Control.LayoutPreset anchors, float left, float top, float right, float bottom)
    {
        control.SetAnchorsPreset(anchors);
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }
}

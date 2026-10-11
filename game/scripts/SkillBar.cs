using System.Collections.Generic;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 右下角的圆形按钮：小号的回城（B）、恢复（V），中号的召唤师技能（F），以及 Q W E R。
/// 按钮内是几何图标，左下角标出按键；英雄技能外圈分段圆环显示等级，有技能点时右上角出现「+」（点击或按 1–4 学习）；
/// 冷却时按扇形遮罩倒计时，回城和恢复进行中外圈显示进度；放不了时外圈红色闪一下。
/// 桌面点击按钮等同按键；触屏时使用独立手指拖动瞄准，右侧大按钮可按住普攻。
/// 站在己方泉水里点召唤师按钮，会弹出召唤师技能选单。
/// </summary>
public partial class SkillBar : Control
{
    public const float BarWidth = 470f;
    public const float BarHeight = 156f;
    public const float TouchWidth = 520f;
    public const float TouchHeight = 440f;

    /// <summary>回城、恢复按钮的编号，接在技能栏位之后。</summary>
    public const int RecallButton = 5;
    public const int RestoreButton = 6;
    const int AttackButton = 7;

    const float RingGap = 0.2f;
    const float RingWidth = 4f;
    const float BadgeRadius = 11f;
    const float PickerRadius = 20f;
    const float TouchBadgeRadius = 16f;
    const float TouchPickerRadius = 24f;
    const float AimStickRadius = 64f;
    const float AimDeadZone = 0.15f;
    const float CancelRadius = 32f;
    const double AttackRepeatSeconds = 0.2;
    const double RejectFlashSeconds = 0.3;
    static readonly string[] Keys = ["Q", "W", "E", "R", "F", "B", "V"];
    static readonly Vector2 CancelCenter = new(450, 45);

    static readonly Color Border = new("#D9D9D7");
    static readonly Color Muted = new("#A0A09E");
    static readonly Color CooldownShade = new(0.24f, 0.24f, 0.24f, 0.35f);

    readonly double[] _rejectLeft = new double[Keys.Length + 1];
    readonly Dictionary<int, TouchCapture> _touches = new();
    bool _pickerOpen;
    bool _touchMode;
    int? _aimFinger;
    int? _attackFinger;
    bool _attackInside;
    bool _cancelHovered;
    double _attackRepeatLeft;
    long _touchCancelSerial;
    Unit? _touchHero;

    sealed class TouchCapture(Vector2 origin, int slot, long cancelSerial)
    {
        public readonly Vector2 Origin = origin;
        public readonly int Slot = slot;
        public readonly long CancelSerial = cancelSerial;
        public Vector2 Position = origin;
        public bool Aiming;
    }

    public bool TouchMode
    {
        get => _touchMode;
        set
        {
            if (_touchMode == value) return;
            ClearTouchCaptures(clearPlayerInput: _touchMode);
            _touchMode = value;
            QueueRedraw();
        }
    }

    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;

    public override void _Ready() => Player.CastRejected += OnCastRejected;

    public override void _ExitTree()
    {
        ClearTouchCaptures(clearPlayerInput: TouchMode);
        if (GodotObject.IsInstanceValid(Player)) Player.CastRejected -= OnCastRejected;
    }

    public override void _Notification(int what)
    {
        if (!TouchMode) return;
        if ((long)what is NotificationApplicationFocusOut or NotificationWMWindowFocusOut or NotificationApplicationPaused
            || what == NotificationVisibilityChanged && !IsVisibleInTree())
            ClearTouchCaptures(clearPlayerInput: true);
    }

    public override void _Process(double delta)
    {
        for (var i = 0; i < _rejectLeft.Length; i++)
            _rejectLeft[i] = Mathf.Max(0, _rejectLeft[i] - delta);
        if (_pickerOpen && !World.CanChangeSummoner(Player.Hero))
            _pickerOpen = false;
        if (TouchMode)
        {
            if (_touches.Count > 0 && (_touchHero != Player.Hero || !Player.Hero.IsAlive))
                ClearTouchCaptures(clearPlayerInput: false);
            if (_aimFinger is { } finger && _touches.TryGetValue(finger, out var aim)
                && Player.AimingSlot != aim.Slot)
            {
                aim.Aiming = false;
                _aimFinger = null;
                _cancelHovered = false;
            }
            if (_attackFinger is not null && _attackInside && _aimFinger is null && Player.AimingSlot is null)
            {
                _attackRepeatLeft -= delta;
                if (_attackRepeatLeft <= 0)
                {
                    Player.AttackNearest();
                    _attackRepeatLeft = AttackRepeatSeconds;
                }
            }
        }
        QueueRedraw();
    }

    void OnCastRejected(int button)
    {
        if ((uint)button < (uint)_rejectLeft.Length) _rejectLeft[button] = RejectFlashSeconds;
    }

    /// <summary>
    /// 桌面 Q W E R 一排；触屏四技能围绕右下角普攻按钮，辅助按钮放在左边。
    /// </summary>
    (Vector2 Center, float Radius) Button(int id) => TouchMode ? id switch
    {
        0 => (new Vector2(288, 346), 40f),
        1 => (new Vector2(246, 242), 40f),
        2 => (new Vector2(300, 140), 40f),
        3 => (new Vector2(422, 164), 44f),
        Unit.SummonerSlot => (new Vector2(155, 350), 32f),
        RecallButton => (new Vector2(62, 330), 27f),
        RestoreButton => (new Vector2(106, 240), 27f),
        AttackButton => (new Vector2(420, 330), 56f),
        _ => (Vector2.Zero, 0f),
    } : id switch
    {
        0 => (new Vector2(130, 96), 34f),
        1 => (new Vector2(220, 96), 34f),
        2 => (new Vector2(310, 96), 34f),
        3 => (new Vector2(412, 90), 40f),
        Unit.SummonerSlot => (new Vector2(40, 108), 28f),
        RecallButton => (new Vector2(24, 48), 19f),
        _ => (new Vector2(70, 30), 19f),
    };

    Vector2 LearnBadge(int slot)
    {
        var (center, radius) = Button(slot);
        return center + new Vector2(radius * 0.78f, -radius * 0.78f);
    }

    /// <summary>桌面召唤师技能选单排在 Q W E 上方；触屏排成左上方的两行。</summary>
    Vector2 PickerSlot(int index) => TouchMode
        ? new(58 + index % 3 * 56, 110 + index / 3 * 56)
        : new(240 + (index - (Summoners.All.Count - 1) / 2f) * 46, 26);

    /// <summary>只有按钮、「+」和召唤师技能选单接收鼠标，控件里的空白处不挡住对地图的点击。</summary>
    public override bool _HasPoint(Vector2 point) => HitTest(point) is not null;

    public override void _GuiInput(InputEvent e)
    {
        if (TouchMode && e is InputEventMouse { Device: -1 })
        {
            AcceptEvent();
            return;
        }
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
            return;
        AcceptEvent();
        var hero = Player.Hero;
        switch (HitTest(click.Position))
        {
            case ("pick", var index):
                World.SetSummoner(hero, Summoners.All[index]);
                _pickerOpen = false;
                break;
            case ("learn", var slot):
                World.LearnSkill(hero, slot);
                break;
            case ("cancel", _):
                CancelOwnedAim();
                break;
            case ("button", AttackButton):
                Player.AttackNearest();
                break;
            case ("button", RecallButton):
                Player.Recall();
                break;
            case ("button", RestoreButton):
                Player.Restore();
                break;
            case ("button", Unit.SummonerSlot) when World.CanChangeSummoner(hero):
                _pickerOpen = !_pickerOpen;
                break;
            case ("button", var slot):
                Player.BeginAim(slot, releaseToCast: false);
                break;
        }
    }

    public override void _Input(InputEvent e)
    {
        if (!TouchMode || !IsVisibleInTree()) return;
        if (e is InputEventMouse { Device: -1 })
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is not InputEventScreenTouch && e is not InputEventScreenDrag) return;
        var cancelSerial = TouchPlatform.ReadCancelSerial();
        if (cancelSerial != _touchCancelSerial)
        {
            // Web 平台的 touchcancel 可能被转成普通松手；变更后的手指不能继续原来的手势。
            ClearTouchCaptures(clearPlayerInput: false);
            _touchCancelSerial = cancelSerial;
        }
        if (e is InputEventScreenTouch touch)
        {
            var point = GetGlobalTransformWithCanvas().AffineInverse() * touch.Position;
            if (touch.Canceled || !touch.Pressed)
            {
                if (!_touches.TryGetValue(touch.Index, out var releasedCapture)) return;
                releasedCapture.Position = point;
                if (releasedCapture.Aiming && _aimFinger == touch.Index)
                {
                    UpdateAim(releasedCapture);
                    Player.ReleaseTouchAim(touch.Canceled || cancelSerial != releasedCapture.CancelSerial || _cancelHovered);
                    _aimFinger = null;
                    _cancelHovered = false;
                }
                if (_attackFinger == touch.Index)
                {
                    _attackFinger = null;
                    _attackInside = false;
                    _attackRepeatLeft = 0;
                }
                _touches.Remove(touch.Index);
                GetViewport().SetInputAsHandled();
                return;
            }
            // 平台复用手指编号时，旧捕获不能留在新的按下事件里。
            if (_touches.Remove(touch.Index))
            {
                if (_aimFinger == touch.Index) CancelOwnedAim();
                if (_attackFinger == touch.Index)
                {
                    _attackFinger = null;
                    _attackInside = false;
                    _attackRepeatLeft = 0;
                }
            }
            var hit = HitTest(point);
            if (hit is not { } target) return;
            var capture = new TouchCapture(point, target.Index, cancelSerial);
            _touches[touch.Index] = capture;
            _touchHero = Player.Hero;
            HandleTouchPress(touch.Index, capture, target.Kind, target.Index);
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventScreenDrag drag && _touches.TryGetValue(drag.Index, out var draggedCapture))
        {
            draggedCapture.Position = GetGlobalTransformWithCanvas().AffineInverse() * drag.Position;
            if (draggedCapture.Aiming && _aimFinger == drag.Index) UpdateAim(draggedCapture);
            if (_attackFinger == drag.Index)
            {
                var (center, radius) = Button(AttackButton);
                _attackInside = draggedCapture.Position.DistanceTo(center) <= radius * 1.4f;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    void HandleTouchPress(int finger, TouchCapture capture, string kind, int index)
    {
        var hero = Player.Hero;
        switch ((kind, index))
        {
            case ("pick", _):
                World.SetSummoner(hero, Summoners.All[index]);
                _pickerOpen = false;
                break;
            case ("learn", _):
                World.LearnSkill(hero, index);
                break;
            case ("cancel", _):
                CancelOwnedAim();
                break;
            case ("button", AttackButton):
                if (_attackFinger is not null) break;
                _attackFinger = finger;
                _attackInside = true;
                _attackRepeatLeft = AttackRepeatSeconds;
                if (_aimFinger is null && Player.AimingSlot is null) Player.AttackNearest();
                break;
            case ("button", RecallButton):
                CancelOwnedAim();
                Player.Recall();
                break;
            case ("button", RestoreButton):
                Player.Restore();
                break;
            case ("button", Unit.SummonerSlot) when World.CanChangeSummoner(hero):
                CancelOwnedAim();
                _pickerOpen = !_pickerOpen;
                break;
            case ("button", _):
                // 第二个技能手指不抢走第一个手指的瞄准；非指向技能由 Player 在按下时施放。
                if (_aimFinger is not null) break;
                _pickerOpen = false;
                if (Player.BeginTouchAim(index) && Player.AimingSlot == index)
                {
                    capture.Aiming = true;
                    _aimFinger = finger;
                    _cancelHovered = false;
                    Player.UpdateTouchAim(Vector2.Zero);
                }
                break;
        }
    }

    Vector2 AimStick(TouchCapture capture)
    {
        var stick = (capture.Position - capture.Origin) / AimStickRadius;
        if (stick.LengthSquared() < AimDeadZone * AimDeadZone) return Vector2.Zero;
        return stick.LimitLength();
    }

    void UpdateAim(TouchCapture capture)
    {
        _cancelHovered = capture.Position.DistanceTo(CancelCenter) <= CancelRadius + 8f;
        Player.UpdateTouchAim(AimStick(capture));
    }

    void CancelOwnedAim()
    {
        if (_aimFinger is { } finger)
        {
            if (_touches.TryGetValue(finger, out var captured)) captured.Aiming = false;
            _aimFinger = null;
            if (GodotObject.IsInstanceValid(Player)) Player.ReleaseTouchAim(cancel: true);
        }
        _cancelHovered = false;
    }

    void ClearTouchCaptures(bool clearPlayerInput)
    {
        CancelOwnedAim();
        _touches.Clear();
        _attackFinger = null;
        _attackInside = false;
        _attackRepeatLeft = 0;
        _touchHero = null;
        _pickerOpen = false;
        if (clearPlayerInput && GodotObject.IsInstanceValid(Player)) Player.CancelTouchInput();
    }

    /// <summary>布局重排或系统取消时，只清技能栏拥有的手指；移动输入由 HUD 统一清理。</summary>
    public void CancelGestures() => ClearTouchCaptures(clearPlayerInput: false);

    (string Kind, int Index)? HitTest(Vector2 point)
    {
        var hero = Player.Hero;
        if (TouchMode && _aimFinger is not null && point.DistanceTo(CancelCenter) <= CancelRadius + 8f)
            return ("cancel", 0);
        if (_pickerOpen)
        {
            for (var i = 0; i < Summoners.All.Count; i++)
            {
                if (point.DistanceTo(PickerSlot(i)) <= (TouchMode ? TouchPickerRadius : PickerRadius))
                    return ("pick", i);
            }
        }
        for (var i = 0; i < hero.Def.Skills.Count; i++)
        {
            if (World.CanLearn(hero, i) && point.DistanceTo(LearnBadge(i)) <= (TouchMode ? 24f : BadgeRadius + 2))
                return ("learn", i);
        }
        for (var id = 0; id < Keys.Length; id++)
        {
            var (center, radius) = Button(id);
            var hitRadius = TouchMode && id >= Unit.SummonerSlot ? Mathf.Max(40f, radius + RingWidth * 2) : radius + RingWidth * 2;
            if (point.DistanceTo(center) <= hitRadius)
                return ("button", id);
        }
        if (TouchMode)
        {
            var (center, radius) = Button(AttackButton);
            if (point.DistanceTo(center) <= radius + RingWidth * 2) return ("button", AttackButton);
        }
        return null;
    }

    public override void _Draw()
    {
        var hero = Player.Hero;
        for (var slot = 0; slot < hero.SkillCount; slot++)
            DrawSkill(hero, slot);
        DrawRecall(hero);
        DrawRestore(hero);
        if (TouchMode)
        {
            DrawAttack(hero);
            DrawTouchAim();
        }
        if (_pickerOpen)
            DrawPicker(hero);
    }

    void DrawSkill(Unit hero, int slot)
    {
        var skill = hero.SkillAt(slot);
        var level = hero.SkillLevels[slot];
        var (center, radius) = Button(slot);
        var cooldown = hero.SkillCooldowns[slot];
        var total = SimTime.Ticks(skill.Cooldown.At(level));
        DrawFace(slot, center, radius, skill.Icon, level > 0, Player.AimingSlot == slot,
            cooldown > 0 ? cooldown / (float)total : 0f, cooldown);

        if (slot == Unit.SummonerSlot)
        {
            // 在泉水里可以更换：外圈加一道己方色细环
            if (World.CanChangeSummoner(hero))
                DrawArc(center, radius + 6, 0, Mathf.Tau, 48, Palette.Ally, 2f, true);
            DrawKey(slot);
            return;
        }

        // 等级圆环：每段代表一级，已学的段用己方色
        var segment = (Mathf.Tau - skill.MaxLevel * RingGap) / skill.MaxLevel;
        for (var l = 0; l < skill.MaxLevel; l++)
        {
            var start = -Mathf.Pi / 2 + RingGap / 2 + l * (segment + RingGap);
            DrawArc(center, radius + 8, start, start + segment, 16, l < level ? Palette.Ally : Border, RingWidth, true);
        }

        var font = ThemeDB.FallbackFont;
        if (level > 0)
        {
            var cost = skill.ManaCost.At(level);
            DrawString(font, center + new Vector2(-radius, radius + 26), $"{cost:0}", HorizontalAlignment.Center, radius * 2, TouchMode ? 14 : 11,
                hero.Mana >= cost ? Muted : Palette.Enemy);
        }
        if (World.CanLearn(hero, slot))
        {
            var badge = LearnBadge(slot);
            var badgeRadius = TouchMode ? TouchBadgeRadius : BadgeRadius;
            DrawCircle(badge, badgeRadius, Palette.Ally);
            DrawString(font, badge + new Vector2(-badgeRadius, TouchMode ? 8 : 6), "+", HorizontalAlignment.Center,
                badgeRadius * 2, TouchMode ? 24 : 18, Colors.White);
        }
        DrawKey(slot);
    }

    void DrawRecall(Unit hero)
    {
        var (center, radius) = Button(RecallButton);
        DrawFace(RecallButton, center, radius, "recall", hero.IsAlive, false, 0f, 0);
        if (hero.IsRecalling)
            DrawProgress(center, radius, 1f - hero.RecallTicks / (float)SimTime.Ticks(GameRules.RecallSeconds));
        DrawKey(RecallButton);
    }

    void DrawRestore(Unit hero)
    {
        var (center, radius) = Button(RestoreButton);
        var cooldown = hero.RestoreCooldown;
        DrawFace(RestoreButton, center, radius, "restore", hero.IsAlive, false,
            cooldown / (float)SimTime.Ticks(GameRules.RestoreCooldownSeconds), cooldown);
        if (hero.IsRestoring)
            DrawProgress(center, radius, hero.RestoreTicks / (float)SimTime.Ticks(GameRules.RestoreSeconds));
        DrawKey(RestoreButton);
    }

    void DrawPicker(Unit hero)
    {
        for (var i = 0; i < Summoners.All.Count; i++)
        {
            var at = PickerSlot(i);
            var chosen = hero.Summoner == Summoners.All[i];
            var radius = TouchMode ? TouchPickerRadius : PickerRadius;
            DrawCircle(at, radius, Colors.White);
            DrawArc(at, radius, 0, Mathf.Tau, 48, chosen ? Palette.Ally : Border, chosen ? 3f : 1.5f, true);
            SkillIcons.Draw(this, Summoners.All[i].Icon, at, radius * 0.5f, Palette.Neutral);
        }
    }

    void DrawAttack(Unit hero)
    {
        var (center, radius) = Button(AttackButton);
        DrawFace(AttackButton, center, radius, "arrow", hero.IsAlive, _attackFinger is not null && _attackInside, 0f, 0);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-radius, radius + 24), "普攻", HorizontalAlignment.Center,
            radius * 2, 18, Palette.Neutral);
    }

    void DrawTouchAim()
    {
        if (_aimFinger is not { } finger || !_touches.TryGetValue(finger, out var aim)) return;
        var color = _cancelHovered ? Palette.Enemy : Palette.Ally;
        DrawCircle(aim.Origin, AimStickRadius, new Color(1f, 1f, 1f, 0.25f));
        DrawArc(aim.Origin, AimStickRadius, 0, Mathf.Tau, 64, new Color(color, 0.65f), 2f, true);
        var thumb = aim.Origin + AimStick(aim) * AimStickRadius;
        DrawLine(aim.Origin, thumb, new Color(color, 0.65f), 2f, true);
        DrawCircle(thumb, 14f, Colors.White);
        DrawArc(thumb, 14f, 0, Mathf.Tau, 32, color, 2f, true);
        DrawCircle(CancelCenter, CancelRadius, Colors.White);
        DrawArc(CancelCenter, CancelRadius, 0, Mathf.Tau, 48, _cancelHovered ? Palette.Enemy : Border, 2f, true);
        DrawLine(CancelCenter + new Vector2(-9, -9), CancelCenter + new Vector2(9, 9), color, 2.5f, true);
        DrawLine(CancelCenter + new Vector2(-9, 9), CancelCenter + new Vector2(9, -9), color, 2.5f, true);
        DrawString(ThemeDB.FallbackFont, CancelCenter + new Vector2(-40, 50), "取消", HorizontalAlignment.Center,
            80, 16, _cancelHovered ? Palette.Enemy : Muted);
    }

    /// <summary>按钮的底、外框、图标和冷却遮罩。</summary>
    void DrawFace(int id, Vector2 center, float radius, string icon, bool usable, bool aiming, float cooldownFraction, int cooldownTicks)
    {
        var font = ThemeDB.FallbackFont;
        var rejected = _rejectLeft[id] > 0;
        DrawCircle(center, radius, Colors.White);
        var outline = rejected ? Palette.Enemy : aiming ? Palette.Ally : Border;
        DrawArc(center, radius, 0, Mathf.Tau, 64, outline, rejected || aiming ? 3f : 1.5f, true);
        SkillIcons.Draw(this, icon, center, radius * 0.5f, usable ? Palette.Neutral : Muted);

        if (cooldownTicks > 0)
        {
            DrawColoredPolygon(Sector(center, radius, cooldownFraction), CooldownShade);
            DrawString(font, center + new Vector2(-radius, radius * 0.25f), $"{Mathf.CeilToInt(SimTime.Seconds(cooldownTicks))}",
                HorizontalAlignment.Center, radius * 2, Mathf.RoundToInt(radius * 0.65f), Colors.White);
        }
    }

    /// <summary>按钮左下角的按键标识，最后画，压在圆环之上。</summary>
    void DrawKey(int id)
    {
        if (TouchMode) return;
        var (center, radius) = Button(id);
        var size = Mathf.Max(16f, Mathf.Round(radius * 0.55f));
        var box = new Rect2(center + new Vector2(-radius * 0.85f - size / 2, radius * 0.85f - size / 2), size, size);
        DrawRect(box, Palette.Neutral);
        DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(0, size * 0.75f), Keys[id], HorizontalAlignment.Center, size,
            Mathf.RoundToInt(size * 0.65f), Colors.White);
    }

    void DrawProgress(Vector2 center, float radius, float fraction) =>
        DrawArc(center, radius + 5, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * fraction, 48, Palette.Ally, 3f, true);

    /// <summary>从正上方顺时针展开、占整圆 fraction 的扇形。</summary>
    static Vector2[] Sector(Vector2 center, float radius, float fraction)
    {
        const int steps = 32;
        var count = Mathf.Max(2, Mathf.CeilToInt(steps * fraction) + 1);
        var points = new Vector2[count + 1];
        points[0] = center;
        for (var i = 0; i < count; i++)
        {
            var angle = -Mathf.Pi / 2 + Mathf.Tau * fraction * i / (count - 1);
            points[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 玩家操作：右键移动或攻击；按住 Q W E R 或 F（召唤师技能）显示技能范围，松开即朝鼠标位置释放（右键或 Esc 取消）；
/// 点击技能按钮进入瞄准，左键确认。B 回城，V 恢复，数字键 1–4 学习对应技能，Tab 切换英雄，S 停止。
/// 调试：F1 升一级；Ctrl + 左键 / 右键把鼠标处 3×3 格子抬高 / 降低 2 级，持续 5 秒。
/// </summary>
public partial class PlayerController : Node3D
{
    const float RayStep = 0.04f;
    const float PickRadius = 0.5f;
    const int DebugTerrainTicks = 150;
    const float TouchDeadZone = 0.12f;
    const float TouchAttackLeash = 2f;

    /// <summary>各技能栏位的施法键：Q W E R 对应英雄技能，F 对应召唤师技能。</summary>
    static readonly Key[] SkillKeys = [Key.Q, Key.W, Key.E, Key.R, Key.F];

    static readonly Key[] LearnKeys = [Key.Key1, Key.Key2, Key.Key3, Key.Key4];

    int _heroIndex;
    int? _aimingSlot;
    bool _releaseToCast;
    GridPos? _hoverTile;
    Unit? _hoverUnit;
    bool _touchMode;
    bool _touchAiming;
    Vector2 _touchMove;
    Vector2 _touchAim;
    Vector2 _lastTouchDirection = new(0, 1);
    bool _hasTouchDirection;
    Unit? _touchMoveHero;
    MoveOrder? _touchMoveOrder;
    Unit? _touchAttackHero;
    AttackOrder? _touchAttackOrder;
    int _touchAttackIssuedTick;
    int _lastTouchMoveTick = -1;

    public World World { get; set; } = null!;
    public Camera3D Camera { get; set; } = null!;
    public TileOverlay Overlay { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;
    public IReadOnlyList<Unit> Heroes { get; set; } = [];

    public Unit Hero => Heroes[_heroIndex];
    public int? AimingSlot => _aimingSlot;

    public bool TouchMode
    {
        get => _touchMode;
        set
        {
            if (_touchMode == value) return;
            CancelTouchInput();
            CancelAim();
            _touchMode = value;
            _hoverTile = null;
            _hoverUnit = null;
        }
    }

    /// <summary>
    /// 操作没能执行（技能未学习、冷却、法力不足、落点不合法，回城或恢复不可用）时触发，
    /// 参数为按钮编号：技能栏位，或 SkillBar.RecallButton / RestoreButton。
    /// </summary>
    public event Action<int>? CastRejected;
    public event Action? HeroChanged;

    public override void _Notification(int what)
    {
        if ((long)what is NotificationApplicationFocusOut or NotificationWMWindowFocusOut)
            OnFocusExited();
    }

    public override void _ExitTree() => CancelTouchInput();

    void OnFocusExited()
    {
        CancelTouchInput();
        _aimingSlot = null;
        _releaseToCast = false;
    }

    public override void _Process(double delta)
    {
        if (Heroes.Count == 0) return;
        if (!Hero.IsAlive)
            CancelTouchInput();
        if (TouchMode)
        {
            UpdateTouchMovement();
            KeepTouchAttackNearby();
            if (_touchAiming)
                SetTouchAimTarget();
            DrawOverlay();
            return;
        }
        var frac = (float)Engine.GetPhysicsInterpolationFraction();
        var mouse = GetViewport().GetMousePosition();
        var hit = Pick(mouse);
        _hoverTile = hit is { } h ? WorldSpace.ToGrid(h) : null;
        _hoverUnit = UnitUnderRay(Camera.ProjectRayOrigin(mouse), Camera.ProjectRayNormal(mouse), frac)
            ?? (_hoverTile is { } tile ? StructureAt(tile) : null);
        DrawOverlay();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (TouchMode && e is InputEventMouse { Device: -1 }) return;
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, CtrlPressed: true } click:
                DebugTerrain(click.ButtonIndex);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                if (_aimingSlot is not null)
                    CastAtHover();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                if (_aimingSlot is not null)
                    CancelAim();
                else
                    RightClick();
                break;
            case InputEventKey { Pressed: false } release when _releaseToCast
                && _aimingSlot is { } slot && release.Keycode == SkillKeys[slot]:
                CastAtHover();
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key);
                break;
        }
    }

    void HandleKey(InputEventKey key)
    {
        var slot = Array.IndexOf(SkillKeys, key.Keycode);
        if (slot >= 0 && slot < Hero.SkillCount)
        {
            BeginAim(slot, releaseToCast: true);
            return;
        }
        var learn = Array.IndexOf(LearnKeys, key.Keycode);
        if (learn >= 0 && learn < Hero.Def.Skills.Count)
        {
            World.LearnSkill(Hero, learn);
            return;
        }

        switch (key.Keycode)
        {
            case Key.B:
                Recall();
                break;
            case Key.V:
                Restore();
                break;
            case Key.Escape:
                CancelAim();
                break;
            case Key.S:
                CancelTouchInput();
                World.OrderStop(Hero);
                break;
            case Key.Tab:
                SwitchHero();
                break;
            case Key.F1:
                World.DebugLevelUp(Hero);
                break;
        }
    }

    public void Recall()
    {
        CancelTouchInput();
        _aimingSlot = null;
        if (!World.OrderRecall(Hero))
            CastRejected?.Invoke(SkillBar.RecallButton);
    }

    public void Restore()
    {
        if (!World.UseRestore(Hero))
            CastRejected?.Invoke(SkillBar.RestoreButton);
    }

    /// <summary>
    /// 开始瞄准：方向和格子类技能显示范围，releaseToCast 为 true 时松开按键即释放，否则等左键确认；
    /// 无目标技能立即释放。放不了时触发 CastRejected。
    /// </summary>
    public void BeginAim(int slot, bool releaseToCast)
        => TryBeginAim(slot, releaseToCast, touch: false);

    bool TryBeginAim(int slot, bool releaseToCast, bool touch)
    {
        if (Heroes.Count == 0 || slot < 0 || slot >= Hero.SkillCount
            || World.CheckCast(Hero, slot) is not null)
        {
            CastRejected?.Invoke(slot);
            return false;
        }
        _touchAiming = false;
        _aimingSlot = null;
        _releaseToCast = false;
        if (Hero.SkillAt(slot).Targeting == SkillTargeting.None)
        {
            if (World.OrderCast(Hero, slot, Hero.Pos) is not null)
            {
                CastRejected?.Invoke(slot);
                return false;
            }
            return true;
        }
        _aimingSlot = slot;
        _releaseToCast = releaseToCast;
        _touchAiming = touch;
        return true;
    }

    void CastAtHover()
    {
        if (_aimingSlot is { } slot && _hoverTile is { } target && World.OrderCast(Hero, slot, target) is not null)
            CastRejected?.Invoke(slot);
        _aimingSlot = null;
        _touchAiming = false;
        _releaseToCast = false;
    }

    void CancelAim()
    {
        _aimingSlot = null;
        _touchAiming = false;
        _touchAim = Vector2.Zero;
        _releaseToCast = false;
        if (TouchMode) _hoverTile = null;
    }

    /// <summary>屏幕方向摇杆，X 向右、Y 向下；Zero 仅停止摇杆自己发出的移动订单。</summary>
    public void SetTouchMove(Vector2 stick)
    {
        var move = stick.LengthSquared() < TouchDeadZone * TouchDeadZone ? Vector2.Zero : stick;
        if (move == Vector2.Zero)
        {
            _touchMove = Vector2.Zero;
            StopTouchMove();
            return;
        }
        if (move != _touchMove)
        {
            // 明确重新拖动左摇杆可覆盖攻击；保持不动的左手仍保留其物理意图。
            _touchAttackHero = null;
            _touchAttackOrder = null;
        }
        _touchMove = move;
        _lastTouchDirection = ScreenToGridDirection(move).Normalized();
        _hasTouchDirection = true;
        _lastTouchMoveTick = -1;
        UpdateTouchMovement();
    }

    public bool BeginTouchAim(int slot)
    {
        if (!TryBeginAim(slot, releaseToCast: false, touch: true)) return false;
        _touchAim = Vector2.Zero;
        if (_touchAiming) SetTouchAimTarget();
        return true;
    }

    public void UpdateTouchAim(Vector2 stick)
    {
        if (!_touchAiming) return;
        // 松手时的 Zero 不抹去最后一次有效拖动，轻点则使用默认目标。
        if (stick.LengthSquared() >= TouchDeadZone * TouchDeadZone)
        {
            _touchAim = stick.Normalized() * Mathf.Min(1f, stick.Length());
            _lastTouchDirection = ScreenToGridDirection(stick).Normalized();
            _hasTouchDirection = true;
        }
        SetTouchAimTarget();
    }

    public void ReleaseTouchAim(bool cancel)
    {
        if (!_touchAiming) return;
        if (!cancel && Hero.IsAlive)
        {
            SetTouchAimTarget();
            CastAtHover();
        }
        _touchAiming = false;
        _touchAim = Vector2.Zero;
        _aimingSlot = null;
        _releaseToCast = false;
        _hoverTile = null;
    }

    public void AttackNearest()
    {
        if (Heroes.Count == 0 || !Hero.IsAlive || Hero.IsStunned || Hero.IsAirborne
            || Hero.IsCasting || Hero.IsRecalling || Hero.Order is CastOrder || _touchAiming) return;
        // 重复按住时保留当前合法目标，避免重置路径或不断切换攻击目标。
        if (Hero.Order is AttackOrder attack && IsTouchAttackTarget(attack.Target)) return;
        // Sim 没有独立攻击前摇：攻击发生在逻辑更新时，冷却期让左摇杆继续移动。
        if (_touchMove != Vector2.Zero && Hero.AttackCooldown > 0) return;
        Unit? best = null;
        var distance = float.MaxValue;
        var bestPriority = 2;
        foreach (var unit in World.Units)
        {
            if (!IsTouchAttackTarget(unit)) continue;
            var priority = unit.Team == Hero.Team ? 1 : 0;
            var d = HeightFall.Sim.World.Distance(Hero, unit);
            if (priority < bestPriority || (priority == bestPriority && d < distance))
            {
                best = unit;
                distance = d;
                bestPriority = priority;
            }
        }
        if (best is null) return;
        StopTouchMove();
        if (World.OrderAttack(Hero, best))
        {
            _touchAttackHero = Hero;
            _touchAttackOrder = Hero.Order as AttackOrder;
            _touchAttackIssuedTick = World.Tick;
        }
    }

    public void SwitchHero()
    {
        CancelTouchInput();
        if (Heroes.Count == 0) return;
        _heroIndex = (_heroIndex + 1) % Heroes.Count;
        _aimingSlot = null;
        _releaseToCast = false;
        _hoverTile = null;
        _hoverUnit = null;
        HeroChanged?.Invoke();
    }

    public void CancelTouchInput()
    {
        _touchMove = Vector2.Zero;
        StopTouchMove();
        if (_touchAttackHero is { } hero && ReferenceEquals(hero.Order, _touchAttackOrder))
            World.OrderStop(hero);
        _touchAttackHero = null;
        _touchAttackOrder = null;
        _aimingSlot = null;
        _touchAiming = false;
        _touchAim = Vector2.Zero;
        _releaseToCast = false;
        _lastTouchMoveTick = -1;
        if (TouchMode)
        {
            _hoverTile = null;
            _hoverUnit = null;
        }
    }

    void StopTouchMove()
    {
        if (_touchMoveHero is { } hero && ReferenceEquals(hero.Order, _touchMoveOrder))
            World.OrderStop(hero);
        _touchMoveHero = null;
        _touchMoveOrder = null;
    }

    void UpdateTouchMovement()
    {
        if (_touchMove == Vector2.Zero || Heroes.Count == 0
            || !Hero.IsAlive || Hero.IsStunned || Hero.IsAirborne
            || Hero.IsCasting || Hero.IsRecalling || Hero.Order is CastOrder) return;
        // 攻击刚接受时不能在同一显示帧被连续摇杆抢掉；已迈出的格子先自然走完。
        if (_touchAttackHero == Hero && ReferenceEquals(Hero.Order, _touchAttackOrder)
            && (World.Tick == _touchAttackIssuedTick || Hero.IsStepping)) return;
        if (_lastTouchMoveTick == World.Tick) return;
        _lastTouchMoveTick = World.Tick;
        var direction = ScreenToGridDirection(_touchMove);
        var step = GridShapes.Direction8(default,
            new GridPos(Mathf.RoundToInt(direction.X * 100), Mathf.RoundToInt(direction.Y * 100)));
        var target = Hero.Pos + step;
        if (step == default || !World.Map.CanStep(Hero.Pos, target)
            || World.UnitAt(target) is { IsStructure: true })
        {
            StopTouchMove();
            return;
        }
        if (Hero.Order is MoveOrder move && move.Target == target) return;
        // Pos 更新在迈步开始时；预排下一格不改动当前 StepTicksLeft 或显示位置。
        World.OrderMove(Hero, target);
        _touchMoveHero = Hero;
        _touchMoveOrder = Hero.Order as MoveOrder;
        _touchAttackHero = null;
        _touchAttackOrder = null;
    }

    Vector2 ScreenToGridDirection(Vector2 stick)
    {
        var basis = Camera.GlobalTransform.Basis;
        var right = new Vector2(basis.X.X, -basis.X.Z).Normalized();
        var up = new Vector2(basis.Y.X, -basis.Y.Z).Normalized();
        if (up == Vector2.Zero) up = new Vector2(-basis.Z.X, basis.Z.Z).Normalized();
        return right * stick.X - up * stick.Y;
    }

    void SetTouchAimTarget()
    {
        if (_aimingSlot is not { } slot || !Hero.IsAlive) return;
        var skill = Hero.SkillAt(slot);
        var range = skill.Area is LineSelector line ? line.Length : Mathf.Max(1f, skill.Range);
        if (_touchAim == Vector2.Zero && !_hasTouchDirection)
        {
            Unit? nearest = null;
            var distance = range;
            foreach (var unit in World.Units)
            {
                if (unit.Team == Hero.Team || !World.CanAttack(Hero, unit)) continue;
                var d = HeightFall.Sim.World.Distance(Hero, unit);
                if (d <= distance && IsOnScreen(unit)) { nearest = unit; distance = d; }
            }
            if (nearest is not null)
            {
                _hoverTile = nearest.NearestTileTo(Hero.Pos);
                _hoverUnit = null;
                return;
            }
        }
        var direction = _touchAim == Vector2.Zero ? _lastTouchDirection : ScreenToGridDirection(_touchAim).Normalized();
        if (skill.Targeting == SkillTargeting.Direction)
        {
            // Direction 只需要一个同方向代理格；逐轴裁剪远处落点会在地图边缘偏转。
            var step = GridShapes.Direction8(default,
                new GridPos(Mathf.RoundToInt(direction.X * 100), Mathf.RoundToInt(direction.Y * 100)));
            _hoverTile = Hero.Pos + step;
            _hoverUnit = null;
            return;
        }
        var length = range * (_touchAim == Vector2.Zero ? 1f : _touchAim.Length());
        var offset = new GridPos(Mathf.RoundToInt(direction.X * length), Mathf.RoundToInt(direction.Y * length));
        if (skill.Targeting == SkillTargeting.Tile)
        {
            // 格子取整后仍在射程内，松手不会意外产生追到射程内的施法移动。
            while (offset.DistanceTo(default) > range)
                offset = Math.Abs(offset.X) > Math.Abs(offset.Y)
                    ? new GridPos(offset.X - Math.Sign(offset.X), offset.Y)
                    : new GridPos(offset.X, offset.Y - Math.Sign(offset.Y));
        }
        var target = Hero.Pos + offset;
        _hoverTile = new GridPos(Math.Clamp(target.X, 0, World.Map.SizeX - 1), Math.Clamp(target.Y, 0, World.Map.SizeY - 1));
        _hoverUnit = null;
    }

    bool IsOnScreen(Unit unit)
    {
        var position = WorldSpace.TileCenter(World.Map, unit.NearestTileTo(Hero.Pos));
        return !Camera.IsPositionBehind(position)
            && GetViewport().GetVisibleRect().HasPoint(Camera.UnprojectPosition(position));
    }

    bool IsTouchAttackTarget(Unit unit) => World.CanAttack(Hero, unit)
        && HeightFall.Sim.World.Distance(Hero, unit) <= Hero.Def.AttackRange + TouchAttackLeash
        && IsOnScreen(unit);

    void KeepTouchAttackNearby()
    {
        if (_touchAttackHero != Hero || _touchAttackOrder is null
            || !ReferenceEquals(Hero.Order, _touchAttackOrder)) return;
        if (!IsTouchAttackTarget(_touchAttackOrder.Target))
        {
            World.OrderStop(Hero);
            _touchAttackHero = null;
            _touchAttackOrder = null;
        }
    }

    void RightClick()
    {
        if (_hoverUnit is { } unit && unit != Hero && World.OrderAttack(Hero, unit))
            return;
        if (_hoverTile is { } tile)
            World.OrderMove(Hero, tile);
    }

    void DrawOverlay()
    {
        Overlay.Clear();
        foreach (var t in World.Telegraphs)
            Overlay.Add(t.Tiles, Palette.ForTeam(t.Team, Viewer) with { A = 0.4f });

        if (_aimingSlot is { } slot && Hero.IsAlive)
        {
            var skill = Hero.SkillAt(slot);
            if (skill.Targeting == SkillTargeting.Tile)
                Overlay.Add(GridShapes.Circle(Hero.Pos, skill.Range), Palette.Ally with { A = 0.08f });
            if (_hoverTile is { } target)
            {
                // 落点不合法时预览变红，此时确认不会放出技能
                var color = World.CanTarget(Hero, slot, target) ? Palette.Ally : Palette.Enemy;
                Overlay.Add(World.PreviewSkill(Hero, slot, target), color with { A = 0.45f });
            }
        }
        else if (_hoverUnit is { } unit && unit != Hero && World.CanAttack(Hero, unit))
        {
            // 指着可攻击的目标时，它脚下的格子变红，右键即攻击
            Overlay.Add(unit.Footprint(), Palette.Enemy with { A = 0.35f });
        }
        else if (_hoverTile is { } tile)
        {
            Overlay.Add(tile, Palette.Ally with { A = 0.2f });
        }
        Overlay.Commit();
    }

    void DebugTerrain(MouseButton button)
    {
        var delta = button switch
        {
            MouseButton.Left => +2,
            MouseButton.Right => -2,
            _ => 0,
        };
        if (delta == 0 || _hoverTile is not { } center)
            return;
        foreach (var p in GridShapes.Circle(center, 1.5f).Where(World.Map.InBounds))
            World.Map.AddHeightModifier(p, delta, DebugTerrainTicks);
    }

    /// <summary>
    /// 鼠标射线碰到的可移动单位：把单位看成一根竖直的柱子，取离射线最近、且在拾取半径内的那个。
    /// 这样点在单位身体上也能选中，而不是落到它背后的地面上。
    /// </summary>
    Unit? UnitUnderRay(Vector3 origin, Vector3 dir, float frac)
    {
        Unit? best = null;
        var bestDistance = PickRadius;
        foreach (var u in World.Units)
        {
            if (!u.IsAlive || u.IsStructure)
                continue;
            var baseAt = WorldSpace.UnitPosition(World.Map, u, frac);
            var height = u.IsHero ? 1.3f : 0.9f;
            for (var s = 0.15f; s <= 1f; s += 0.25f)
            {
                var toPoint = baseAt + Vector3.Up * (height * s) - origin;
                var d = (toPoint - dir * toPoint.Dot(dir)).Length();
                if (d < bestDistance)
                {
                    best = u;
                    bestDistance = d;
                }
            }
        }
        return best;
    }

    Unit? StructureAt(GridPos tile) =>
        World.UnitAt(tile) is { IsStructure: true, Def.Invulnerable: false } structure ? structure : null;

    /// <summary>沿鼠标射线步进，返回第一个低于格子顶面的点。</summary>
    Vector3? Pick(Vector2 screen)
    {
        var origin = Camera.ProjectRayOrigin(screen);
        var dir = Camera.ProjectRayNormal(screen);
        if (dir.Y >= 0)
            return null;

        // 升起动画会略微越过最高级，所以从稍高处开始步进
        var top = WorldSpace.Elevation(HeightRules.Max) + 0.5f;
        for (var t = Mathf.Max(0f, (origin.Y - top) / -dir.Y); ; t += RayStep)
        {
            var point = origin + dir * t;
            if (point.Y < 0)
                return null;
            var tile = WorldSpace.ToGrid(point);
            if (World.Map.InBounds(tile) && point.Y <= WorldSpace.GroundAt(World.Map, tile))
                return point;
        }
    }
}

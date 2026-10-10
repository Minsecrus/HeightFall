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

    /// <summary>各技能栏位的施法键：Q W E R 对应英雄技能，F 对应召唤师技能。</summary>
    static readonly Key[] SkillKeys = [Key.Q, Key.W, Key.E, Key.R, Key.F];

    static readonly Key[] LearnKeys = [Key.Key1, Key.Key2, Key.Key3, Key.Key4];

    int _heroIndex;
    int? _aimingSlot;
    bool _releaseToCast;
    GridPos? _hoverTile;
    Unit? _hoverUnit;

    public World World { get; set; } = null!;
    public Camera3D Camera { get; set; } = null!;
    public TileOverlay Overlay { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;
    public IReadOnlyList<Unit> Heroes { get; set; } = [];

    public Unit Hero => Heroes[_heroIndex];
    public int? AimingSlot => _aimingSlot;

    /// <summary>
    /// 操作没能执行（技能未学习、冷却、法力不足、落点不合法，回城或恢复不可用）时触发，
    /// 参数为按钮编号：技能栏位，或 SkillBar.RecallButton / RestoreButton。
    /// </summary>
    public event Action<int>? CastRejected;

    public override void _Process(double delta)
    {
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
                    _aimingSlot = null;
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
                _aimingSlot = null;
                break;
            case Key.S:
                World.OrderStop(Hero);
                break;
            case Key.Tab:
                _heroIndex = (_heroIndex + 1) % Heroes.Count;
                _aimingSlot = null;
                break;
            case Key.F1:
                World.DebugLevelUp(Hero);
                break;
        }
    }

    public void Recall()
    {
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
    {
        if (World.CheckCast(Hero, slot) is not null)
        {
            CastRejected?.Invoke(slot);
            return;
        }
        if (Hero.SkillAt(slot).Targeting == SkillTargeting.None)
        {
            World.OrderCast(Hero, slot, Hero.Pos);
            return;
        }
        _aimingSlot = slot;
        _releaseToCast = releaseToCast;
    }

    void CastAtHover()
    {
        if (_aimingSlot is { } slot && _hoverTile is { } target && World.OrderCast(Hero, slot, target) is not null)
            CastRejected?.Invoke(slot);
        _aimingSlot = null;
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

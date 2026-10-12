using System;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 格子坐标与世界坐标的换算：1 格 = 1 m，格子 (x, y) 占据世界 X ∈ [x, x+1]、Z ∈ [-(y+1), -y]，
/// 北方为 -Z，每级高度 0.2 m。
/// </summary>
public static class WorldSpace
{
    public const float LevelHeight = 0.2f;
    public const float LeapArcHeight = 2f;

    /// <summary>地形的显示高度（米，含升降动画），由 MapView 提供；未提供时用逻辑高度。</summary>
    public static Func<GridPos, float>? TerrainElevation { get; set; }

    public static float Elevation(int height) => height * LevelHeight;

    public static float GroundAt(GameMap map, GridPos p) =>
        TerrainElevation?.Invoke(p) ?? (map.InBounds(p) ? Elevation(map.HeightAt(p)) : 0f);

    public static Vector3 TileCenter(GameMap map, GridPos p) =>
        new(p.X + 0.5f, GroundAt(map, p), -(p.Y + 0.5f));

    /// <summary>Size×Size 区域的中心，Origin 为西南角格子。</summary>
    public static Vector3 AreaCenter(GameMap map, GridPos origin, int size) =>
        new(origin.X + size / 2f, GroundAt(map, origin), -(origin.Y + size / 2f));

    public static GridPos ToGrid(Vector3 world) =>
        new(Mathf.FloorToInt(world.X), Mathf.FloorToInt(-world.Z));

    /// <summary>
    /// 单位在画面中的位置。frac 是距上一个逻辑帧经过的比例，用于两帧之间的平滑插值。
    /// 走下高崖时先迈出崖边，再加速掉下去。
    /// </summary>
    public static Vector3 UnitPosition(GameMap map, Unit u, float frac)
    {
        if (u.IsStructure)
            return AreaCenter(map, u.Pos, u.Size);
        if (u.IsAirborne)
        {
            var t = Mathf.Clamp(1f - (u.AirborneTicks - frac) / u.AirborneTotal, 0f, 1f);
            var from = TileCenter(map, u.AirborneFrom);
            var to = TileCenter(map, u.Pos);
            return from.Lerp(to, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * LeapArcHeight);
        }

        var a = TileCenter(map, u.PrevPos);
        var b = TileCenter(map, u.Pos);
        var s = u.IsStepping ? Mathf.Clamp(1f - (u.StepTicksLeft - frac) / u.StepTicks, 0f, 1f) : 1f;
        float horizontal, y;
        if (a.Y - b.Y >= HeightRules.FallThreshold * LevelHeight - 0.01f)
        {
            var fall = Mathf.Clamp((s - 0.45f) / 0.55f, 0f, 1f);
            y = Mathf.Lerp(a.Y, b.Y, fall * fall);
            horizontal = Mathf.Clamp(s / 0.6f, 0f, 1f);
        }
        else
        {
            y = Mathf.Lerp(a.Y, b.Y, s);
            horizontal = s;
        }
        return new Vector3(Mathf.Lerp(a.X, b.X, horizontal), y, Mathf.Lerp(a.Z, b.Z, horizontal));
    }

    /// <summary>投射物按逻辑层的真实弹道绘制，高度从级换算成米。</summary>
    public static Vector3 ProjectilePosition(Projectile p, float frac)
    {
        var x = Mathf.Lerp(p.PrevPosition.X, p.Position.X, frac);
        var y = Mathf.Lerp(p.PrevPosition.Y, p.Position.Y, frac);
        return new Vector3(x, Mathf.Lerp(p.PrevHeight, p.Height, frac) * LevelHeight, -y);
    }
}

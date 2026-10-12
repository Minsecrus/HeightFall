using System.Numerics;

namespace HeightFall.Sim;

/// <summary>
/// 普攻投射物的弹道，高度以级为单位（1 级 = 0.2 m）。投射物从攻击者所在格高度加出手高度出发，
/// 飞向目标所在格高度加模型一半高度。直线弹道沿两点连线飞行；抛物线弹道在连线上方拱起，距离越远拱得越高。
/// 飞行路径上任一点下方的地形顶面高于弹道时，投射物撞上地形。
/// </summary>
public static class Ballistics
{
    /// <summary>抛物线弹道的拱高：水平距离每格拱起的级数。</summary>
    const float ArcPerTile = 1.2f;

    /// <summary>沿路径检查地形的间隔（格）。</summary>
    const float SampleStep = 0.25f;

    public static float LaunchHeight(GameMap map, Unit shooter) => map.HeightAt(shooter.Pos) + shooter.Def.LaunchHeight;

    public static float AimHeight(GameMap map, Unit target) => map.HeightAt(target.Pos) + target.Def.BodyHeight / 2f;

    public static float ArcHeight(Unit shooter, float distance) => shooter.Def.ArcingProjectile ? distance * ArcPerTile : 0f;

    /// <summary>飞行进度 t（0–1）处的弹道高度。</summary>
    public static float HeightAt(float from, float to, float arc, float t) => from + (to - from) * t + 4f * arc * t * (1f - t);

    /// <summary>该点下方的地形是否高于弹道。</summary>
    public static bool HitsTerrain(GameMap map, Vector2 point, float height)
    {
        var tile = new GridPos((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));
        return map.InBounds(tile) && map.HeightAt(tile) > height;
    }

    /// <summary>按目标当前位置，从 shooter 打出的投射物能否不撞地形地飞到目标。</summary>
    public static bool IsClear(GameMap map, Unit shooter, Unit target)
    {
        var from = shooter.Center;
        var to = target.Center;
        var distance = Vector2.Distance(from, to);
        var start = LaunchHeight(map, shooter);
        var end = AimHeight(map, target);
        var arc = ArcHeight(shooter, distance);
        var steps = Math.Max(1, (int)MathF.Ceiling(distance / SampleStep));
        for (var i = 1; i < steps; i++)
        {
            var t = i / (float)steps;
            if (HitsTerrain(map, Vector2.Lerp(from, to, t), HeightAt(start, end, arc, t)))
                return false;
        }
        return true;
    }
}

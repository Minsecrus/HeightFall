using System.Numerics;

namespace HeightFall.Sim;

/// <summary>追踪目标的普攻投射物，坐标为格子坐标系，高度以级为单位。弹道见 Ballistics。</summary>
public sealed class Projectile
{
    internal Projectile(int id, Unit source, Unit target, float damage, float speedTilesPerSecond, float launchHeight, float arc)
    {
        Id = id;
        Source = source;
        Target = target;
        Damage = damage;
        SpeedPerTick = speedTilesPerSecond / SimTime.TicksPerSecond;
        Position = PrevPosition = source.Center;
        Height = PrevHeight = LaunchHeight = launchHeight;
        Arc = arc;
    }

    public int Id { get; }
    public Unit Source { get; }
    public Unit Target { get; }
    public Vector2 Position { get; internal set; }
    public Vector2 PrevPosition { get; internal set; }
    public float Height { get; internal set; }
    public float PrevHeight { get; internal set; }
    public bool IsDone { get; internal set; }

    internal float Damage { get; }
    internal float SpeedPerTick { get; }
    internal float LaunchHeight { get; }

    /// <summary>抛物线弹道的拱高，直线弹道为 0；出手时按距离确定。</summary>
    internal float Arc { get; }

    /// <summary>已经飞过的水平距离（格）。</summary>
    internal float Traveled { get; set; }
}

/// <summary>延迟生效的技能落点，在生效前显示给双方。</summary>
public sealed record Telegraph(Team Team, IReadOnlyList<GridPos> Tiles, int EndTick);

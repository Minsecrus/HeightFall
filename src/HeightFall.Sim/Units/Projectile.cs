using System.Numerics;

namespace HeightFall.Sim;

/// <summary>追踪目标的普攻投射物，坐标为格子坐标系。</summary>
public sealed class Projectile
{
    internal Projectile(int id, Unit source, Unit target, float damage, float speedTilesPerSecond)
    {
        Id = id;
        Source = source;
        Target = target;
        Damage = damage;
        SpeedPerTick = speedTilesPerSecond / SimTime.TicksPerSecond;
        Position = PrevPosition = source.Center;
    }

    public int Id { get; }
    public Unit Source { get; }
    public Unit Target { get; }
    public Vector2 Position { get; internal set; }
    public Vector2 PrevPosition { get; internal set; }
    public bool IsDone { get; internal set; }

    internal float Damage { get; }
    internal float SpeedPerTick { get; }
}

/// <summary>延迟生效的技能落点，在生效前显示给双方。</summary>
public sealed record Telegraph(Team Team, IReadOnlyList<GridPos> Tiles, int EndTick);

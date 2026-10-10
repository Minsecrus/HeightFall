namespace HeightFall.Sim;

public abstract record Order;

public sealed record MoveOrder(GridPos Target) : Order;

public sealed record AttackOrder(Unit Target) : Order;

/// <summary>Target 为目标格；方向类技能取从施法者指向该格的方向。</summary>
public sealed record CastOrder(int Slot, GridPos Target) : Order;

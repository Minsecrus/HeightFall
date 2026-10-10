namespace HeightFall.Sim;

/// <summary>技能的目标格子集合。</summary>
public static class GridShapes
{
    static readonly GridPos[] Directions8 =
    [
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
        new(-1, 0), new(-1, -1), new(0, -1), new(1, -1),
    ];

    /// <summary>从 from 指向 to 的方向，取最近的 8 方向之一；两点重合时返回 (0, 0)。</summary>
    public static GridPos Direction8(GridPos from, GridPos to)
    {
        if (from == to)
            return default;
        var angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var octant = (int)MathF.Round(angle / (MathF.PI / 4));
        return Directions8[(octant + 8) % 8];
    }

    /// <summary>格子中心距离 ≤ radius 的格子：r = 1 为十字形 5 格，r = 1.5 为 3×3。</summary>
    public static IEnumerable<GridPos> Circle(GridPos center, float radius)
    {
        var r = (int)MathF.Floor(radius);
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            if (dx * dx + dy * dy <= radius * radius)
                yield return new GridPos(center.X + dx, center.Y + dy);
        }
    }
}

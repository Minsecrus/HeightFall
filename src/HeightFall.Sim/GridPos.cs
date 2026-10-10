namespace HeightFall.Sim;

/// <summary>格子坐标：原点在西南角，x 向东，y 向北。</summary>
public readonly record struct GridPos(int X, int Y)
{
    public static readonly GridPos[] Neighbors8 =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    ];

    public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);

    public static GridPos operator -(GridPos a, GridPos b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>格子中心之间的欧氏距离。</summary>
    public float DistanceTo(GridPos other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

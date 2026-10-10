namespace HeightFall.Sim;

/// <summary>标准地图的对称变换。</summary>
public static class MapSymmetry
{
    /// <summary>中心对称：「岭」↔「渊」。</summary>
    public static GridPos Rotate(GridPos p, int size) => new(size - 1 - p.X, size - 1 - p.Y);

    /// <summary>对角线 x = y 对称：上路 ↔ 下路。</summary>
    public static GridPos Transpose(GridPos p) => new(p.Y, p.X);

    public static IEnumerable<GridPos> Images(GridPos p, int size)
    {
        yield return p;
        yield return Transpose(p);
        yield return Rotate(p, size);
        yield return Rotate(Transpose(p), size);
    }
}

namespace HeightFall.Sim;

public sealed record MapLayout(GameMap Map, IReadOnlyList<Structure> Structures, IReadOnlyList<GridPos> BossPits);

/// <summary>
/// 按 docs/design.md 生成标准地图。地图关于中心点和对角线 x = y 都对称，
/// 地形只描述一部分，再用四重对称铺满全图；建筑只描述「岭」方，再中心对称给「渊」。
/// </summary>
public static class StandardMap
{
    public const int Size = 128;
    const int BorderWidth = 4;

    // 「岭」方上路野区（中路以北、河道以南）的岩壁与 3 级台地：x, y, 宽, 高
    static readonly (int X, int Y, int W, int H)[] Rocks =
    [
        (18, 40, 6, 3), (16, 52, 3, 10), (24, 50, 8, 3), (30, 44, 3, 6),
        (20, 70, 10, 3), (34, 60, 3, 8), (40, 66, 6, 3), (42, 72, 3, 4),
        (17, 84, 3, 8), (26, 80, 8, 3), (20, 96, 4, 3),
    ];

    static readonly (int X, int Y, int W, int H)[] Ledges =
    [
        (22, 58, 5, 5), (26, 88, 4, 4),
    ];

    // 防御塔锚点（占地中心附近的格子），按 T3、T2、T1 排列
    static readonly (Lane Lane, (int X, int Y)[] Anchors)[] Towers =
    [
        (Lane.Top, [(11, 36), (11, 62), (11, 90)]),
        (Lane.Mid, [(35, 35), (45, 45), (54, 54)]),
        (Lane.Bot, [(36, 11), (62, 11), (90, 11)]),
    ];

    // 每路一座兵营，位于出口坡道内侧、正对兵线
    static readonly (Lane Lane, (int X, int Y) Anchor)[] Barracks =
    [
        (Lane.Top, (11, 27)),
        (Lane.Mid, (26, 26)),
        (Lane.Bot, (27, 11)),
    ];

    public static MapLayout Build()
    {
        var map = new GameMap(Size, Size);

        foreach (var p in map.AllTiles())
            map.SetTerrain(p, TerrainKind.Ground, HeightRules.Ground);

        PaintRect(map, 9, 31, 5, 88, TerrainKind.Lane, HeightRules.Ground);   // 上路西段
        PaintRect(map, 9, 114, 88, 5, TerrainKind.Lane, HeightRules.Ground);  // 上路北段
        Paint(map, p => Math.Abs(p.X - p.Y) <= 3 && Within(p.X, 31, 96) && Within(p.Y, 31, 96),
            TerrainKind.Lane, HeightRules.Ground);
        Paint(map, p => Math.Abs(p.X + p.Y - (Size - 1)) <= 4, TerrainKind.River, HeightRules.River);

        PaintRect(map, BorderWidth, BorderWidth, 27, 27, TerrainKind.Plateau, HeightRules.Plateau);
        PaintRect(map, 9, 31, 5, 3, TerrainKind.Ramp, HeightRules.Ramp);
        Paint(map, p => Math.Abs(p.X - p.Y) <= 3 && Within(Math.Max(p.X, p.Y), 31, 33),
            TerrainKind.Ramp, HeightRules.Ramp);

        foreach (var r in Rocks)
            PaintRect(map, r.X, r.Y, r.W, r.H, TerrainKind.Rock, HeightRules.Rock);
        foreach (var r in Ledges)
            PaintRect(map, r.X, r.Y, r.W, r.H, TerrainKind.Ground, HeightRules.Ramp);

        Paint(map, p => p.X < BorderWidth || p.Y < BorderWidth, TerrainKind.Border, HeightRules.Border);

        var heights = HeightsStructures().ToList();
        var structures = heights.Concat(heights.Select(s => s.MirroredTo(Team.Fall, Size))).ToList();
        foreach (var tile in structures.SelectMany(s => s.Footprint()))
            map.SetFixed(tile);

        return new MapLayout(map, structures, [new GridPos(36, 91), new GridPos(91, 36)]);
    }

    static IEnumerable<Structure> HeightsStructures()
    {
        yield return Place(StructureKind.Fountain, Lane.None, 0, (7, 7), 5);
        yield return Place(StructureKind.Ancient, Lane.None, 0, (18, 18), 4);
        yield return Place(StructureKind.Tower, Lane.None, 4, (22, 18), 2);
        yield return Place(StructureKind.Tower, Lane.None, 4, (18, 22), 2);

        foreach (var (lane, anchors) in Towers)
        {
            for (var i = 0; i < anchors.Length; i++)
                yield return Place(StructureKind.Tower, lane, 3 - i, anchors[i], 2);
        }

        foreach (var (lane, anchor) in Barracks)
            yield return Place(StructureKind.Barracks, lane, 0, anchor, 3);
    }

    static Structure Place(StructureKind kind, Lane lane, int tier, (int X, int Y) anchor, int size) =>
        new(kind, Team.Heights, lane, tier, new GridPos(anchor.X - size / 2, anchor.Y - size / 2), size);

    static void PaintRect(GameMap map, int x0, int y0, int w, int h, TerrainKind kind, int height) =>
        Paint(map, p => Within(p.X, x0, x0 + w - 1) && Within(p.Y, y0, y0 + h - 1), kind, height);

    /// <summary>把满足条件的格子及其全部对称像设为指定地形。</summary>
    static void Paint(GameMap map, Func<GridPos, bool> predicate, TerrainKind kind, int height)
    {
        foreach (var p in map.AllTiles().Where(predicate).ToList())
        {
            foreach (var image in MapSymmetry.Images(p, Size))
                map.SetTerrain(image, kind, height);
        }
    }

    static bool Within(int v, int min, int max) => v >= min && v <= max;
}

namespace HeightFall.Sim;

public abstract record TileSelector
{
    public abstract List<GridPos> Select(GameMap map, GridPos caster, GridPos target);
}

/// <summary>
/// 从施法者旁边一格开始，朝目标方向（最近的 8 方向之一）延伸 Length 格。
/// HalfWidth 为中线两侧各加宽的格数：横竖方向向两侧加格；斜向时每行、每列都多出相同格数，形成等宽的阶梯带。
/// StopAtWall 时，中线遇到比施法者高 2 级及以上的格子整条就停下，两侧被挡住的格子单独跳过。
/// </summary>
public sealed record LineSelector(int Length, bool StopAtWall, int HalfWidth = 0) : TileSelector
{
    public override List<GridPos> Select(GameMap map, GridPos caster, GridPos target)
    {
        var tiles = new List<GridPos>();
        var dir = GridShapes.Direction8(caster, target);
        if (dir == default)
            return tiles;
        var casterHeight = map.HeightAt(caster);
        bool Blocked(GridPos p) => StopAtWall && map.HeightAt(p) - casterHeight >= HeightRules.FallThreshold;

        var diagonal = dir.X != 0 && dir.Y != 0;
        for (var k = 1; k <= Length; k++)
        {
            var center = new GridPos(caster.X + dir.X * k, caster.Y + dir.Y * k);
            if (!map.InBounds(center) || Blocked(center))
                break;
            tiles.Add(center);
            for (var i = 1; i <= HalfWidth; i++)
            {
                GridPos[] sides = diagonal
                    ? [new(center.X - dir.X * i, center.Y), new(center.X, center.Y - dir.Y * i)]
                    : [new(center.X - dir.Y * i, center.Y + dir.X * i), new(center.X + dir.Y * i, center.Y - dir.X * i)];
                tiles.AddRange(sides.Where(p => map.InBounds(p) && p != caster && !Blocked(p) && !tiles.Contains(p)));
            }
        }
        return tiles;
    }
}

/// <summary>以目标格为中心、半径为 Radius 的圆形范围。</summary>
public sealed record CircleSelector(float Radius) : TileSelector
{
    public override List<GridPos> Select(GameMap map, GridPos caster, GridPos target) =>
        GridShapes.Circle(target, Radius).Where(map.InBounds).ToList();
}

public sealed record TargetTileSelector : TileSelector
{
    public override List<GridPos> Select(GameMap map, GridPos caster, GridPos target) =>
        map.InBounds(target) ? [target] : [];
}

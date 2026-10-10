namespace HeightFall.Sim;

public enum StructureKind
{
    Tower,
    Barracks,
    Ancient,
    Fountain,
}

public enum Lane
{
    None,
    Top,
    Mid,
    Bot,
}

/// <summary>占地为 Size×Size 的建筑，Origin 是占地西南角的格子。</summary>
public sealed record Structure(StructureKind Kind, Team Team, Lane Lane, int Tier, GridPos Origin, int Size)
{
    public IEnumerable<GridPos> Footprint()
    {
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
            yield return new GridPos(Origin.X + x, Origin.Y + y);
    }

    /// <summary>中心对称到另一方：西侧的上路对应到东侧，成为下路。</summary>
    public Structure MirroredTo(Team team, int mapSize) => this with
    {
        Team = team,
        Lane = Lane switch { Lane.Top => Lane.Bot, Lane.Bot => Lane.Top, _ => Lane },
        Origin = new GridPos(mapSize - Origin.X - Size, mapSize - Origin.Y - Size),
    };
}

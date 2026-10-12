using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class PathfinderTests
{
    static GameMap OpenMap(int size)
    {
        var map = new GameMap(size, size);
        foreach (var p in map.AllTiles())
            map.SetTerrain(p, TerrainKind.Ground, HeightRules.Ground);
        return map;
    }

    [Fact]
    public void BlockedGoalStopsBesideItWithoutSearchingWholeMap()
    {
        var map = OpenMap(128);
        var goal = new GridPos(40, 20);
        var checks = 0;

        var path = new Pathfinder(map).Find(new GridPos(10, 20), goal, p => { checks++; return p == goal; }, allowBlockedGoal: false);

        Assert.Equal(new GridPos(39, 20), path[^1]);
        Assert.True(checks < map.SizeX * map.SizeY, $"blocked checks: {checks}");
    }

    [Fact]
    public void SurroundedGoalFallsBackToDiagonalWithinSearchLimit()
    {
        var map = OpenMap(128);
        var goal = new GridPos(40, 20);
        HashSet<GridPos> occupied = [goal, goal + new GridPos(1, 0), goal - new GridPos(1, 0), goal + new GridPos(0, 1), goal - new GridPos(0, 1)];
        var checks = 0;

        var path = new Pathfinder(map).Find(new GridPos(10, 20), goal, p => { checks++; return occupied.Contains(p); }, allowBlockedGoal: false);

        var end = path[^1];
        Assert.Equal(1, Math.Abs(end.X - goal.X));
        Assert.Equal(1, Math.Abs(end.Y - goal.Y));
        Assert.True(checks < map.SizeX * map.SizeY, $"blocked checks: {checks}");
    }
}

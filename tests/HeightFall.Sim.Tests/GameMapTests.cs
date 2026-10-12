using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class GameMapTests
{
    [Fact]
    public void HeightModifiersStackClampAndExpire()
    {
        var map = new GameMap(4, 4);
        foreach (var p in map.AllTiles())
            map.SetTerrain(p, TerrainKind.Ground, HeightRules.Ground);
        var tile = new GridPos(1, 1);
        var neighbor = new GridPos(2, 1);
        map.SetFixed(new GridPos(3, 3));

        map.AddHeightModifier(tile, +4, durationTicks: 2);
        map.AddHeightModifier(tile, +5, durationTicks: 5);
        Assert.Equal(HeightRules.Max, map.HeightAt(tile));
        Assert.False(map.CanStep(neighbor, tile));
        Assert.True(map.CanStep(tile, neighbor));

        map.Tick();
        map.Tick();
        Assert.Equal(HeightRules.Ground + 5, map.HeightAt(tile));

        for (var i = 0; i < 3; i++)
            map.Tick();
        Assert.Equal(HeightRules.Ground, map.HeightAt(tile));

        Assert.False(map.AddHeightModifier(new GridPos(3, 3), +2, durationTicks: 5));
        map.SetTerrain(new GridPos(0, 0), TerrainKind.Rock, HeightRules.Rock);
        Assert.False(map.CanStep(new GridPos(1, 0), new GridPos(0, 0)));
    }
}

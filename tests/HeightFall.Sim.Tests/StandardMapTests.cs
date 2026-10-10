using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class StandardMapTests
{
    [Fact]
    public void MapAndStructuresAreSymmetric()
    {
        var layout = StandardMap.Build();
        var map = layout.Map;

        foreach (var p in map.AllTiles())
        {
            foreach (var image in MapSymmetry.Images(p, StandardMap.Size))
            {
                Assert.Equal(map.KindAt(p), map.KindAt(image));
                Assert.Equal(map.HeightAt(p), map.HeightAt(image));
            }
        }

        foreach (var team in new[] { Team.Heights, Team.Fall })
        {
            var own = layout.Structures.Where(s => s.Team == team).ToList();
            Assert.Equal(11, own.Count(s => s.Kind == StructureKind.Tower));
            Assert.Equal(3, own.Count(s => s.Kind == StructureKind.Barracks));
            Assert.Single(own, s => s.Kind == StructureKind.Ancient);
            Assert.Single(own, s => s.Kind == StructureKind.Fountain);
        }
    }

    [Fact]
    public void BasesAreConnectedByWalking()
    {
        var layout = StandardMap.Build();
        var map = layout.Map;
        var blocked = layout.Structures.SelectMany(s => s.Footprint()).ToHashSet();
        var start = new GridPos(10, 10);
        var goal = MapSymmetry.Rotate(start, StandardMap.Size);

        var visited = new HashSet<GridPos> { start };
        var queue = new Queue<GridPos>([start]);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var offset in GridPos.Neighbors8)
            {
                var next = p + offset;
                if (!blocked.Contains(next) && map.CanStep(p, next) && visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        Assert.Contains(goal, visited);
    }
}

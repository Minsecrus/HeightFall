using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class BallisticsTests
{
    static readonly GridPos ShooterTile = new(68, 33);
    static readonly GridPos TargetTile = new(74, 33);
    static readonly GridPos WallTile = new(71, 33);

    static (World World, Unit Shooter, Unit Target) Range()
    {
        var world = new World(StandardMap.Build());
        for (var x = ShooterTile.X; x <= TargetTile.X; x++)
            Assert.Equal(HeightRules.Ground, world.Map.HeightAt(new GridPos(x, ShooterTile.Y)));
        var shooter = world.SpawnHero(Heroes.Falcon, Team.Heights);
        var target = world.SpawnHero(Heroes.Bastion, Team.Fall);
        world.Blink(shooter, ShooterTile);
        world.Blink(target, TargetTile);
        return (world, shooter, target);
    }

    static void RaiseWall(World world) =>
        Assert.True(world.Map.AddHeightModifier(WallTile, HeightRules.Rock - HeightRules.Ground, SimTime.Ticks(30)));

    [Fact]
    public void StraightShotsAreBlockedByTerrainThatArcsFlyOver()
    {
        var (world, falcon, target) = Range();
        var siege = world.AddUnit(UnitDefs.SiegeCreep, Team.Heights, ShooterTile + new GridPos(0, 1));
        var siegeTarget = world.AddUnit(UnitDefs.MeleeCreep, Team.Fall, TargetTile + new GridPos(0, 1));
        Assert.True(Ballistics.IsClear(world.Map, falcon, target));
        Assert.True(Ballistics.IsClear(world.Map, siege, siegeTarget));

        RaiseWall(world);
        Assert.True(world.Map.AddHeightModifier(WallTile + new GridPos(0, 1), HeightRules.Rock - HeightRules.Ground, SimTime.Ticks(30)));

        Assert.False(Ballistics.IsClear(world.Map, falcon, target));
        Assert.True(Ballistics.IsClear(world.Map, siege, siegeTarget));
    }

    [Fact]
    public void ProjectileHitsTerrainThatRisesInItsPath()
    {
        var (world, falcon, target) = Range();
        Assert.True(world.OrderAttack(falcon, target));
        while (world.Projectiles.Count == 0)
            world.Step();
        world.DrainEvents();

        RaiseWall(world);
        while (world.Projectiles.Count > 0)
            world.Step();

        Assert.Equal(target.MaxHp, target.Hp);
        Assert.Contains(world.DrainEvents(), e => e is ProjectileBlockedEvent);
    }

    [Fact]
    public void RangedAttackerMovesForAClearShotInsteadOfShootingTheWall()
    {
        var (world, falcon, target) = Range();
        RaiseWall(world);
        Assert.True(world.OrderAttack(falcon, target));

        var events = new List<GameEvent>();
        for (var i = 0; i < SimTime.Ticks(4) && target.Hp >= target.MaxHp; i++)
        {
            world.Step();
            events.AddRange(world.DrainEvents());
        }

        Assert.True(target.Hp < target.MaxHp);
        Assert.NotEqual(ShooterTile, falcon.Pos);
        Assert.DoesNotContain(events, e => e is ProjectileBlockedEvent);
    }
}

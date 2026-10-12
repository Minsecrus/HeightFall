using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class StructureTests
{
    static Unit Building(World world, StructureKind kind, Lane lane, int tier) => world.Units.Single(u =>
        u.Structure is { } s && s.Team == Team.Fall && s.Kind == kind && s.Lane == lane && s.Tier == tier);

    static void Destroy(World world, Unit attacker, Unit building)
    {
        Assert.False(building.IsShielded);
        world.DealDamage(attacker, building, building.MaxHp * 100, DamageType.Physical);
        Assert.False(building.IsAlive);
    }

    [Fact]
    public void BuildingsCanOnlyBeDamagedInAttackOrderAndTheAncientDecidesTheGame()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var t1 = Building(world, StructureKind.Tower, Lane.Mid, 1);
        var t2 = Building(world, StructureKind.Tower, Lane.Mid, 2);
        var t3 = Building(world, StructureKind.Tower, Lane.Mid, 3);
        var barracks = Building(world, StructureKind.Barracks, Lane.Mid, 0);
        var t4s = world.Units.Where(u => u.Structure is { Kind: StructureKind.Tower, Tier: 4 } s && s.Team == Team.Fall).ToList();
        var ancient = world.Units.Single(u => u.Structure is { Kind: StructureKind.Ancient } s && s.Team == Team.Fall);

        world.DealDamage(hero, t2, 500, DamageType.Physical);
        Assert.Equal(t2.MaxHp, t2.Hp);
        Assert.False(world.CanAttack(hero, t2));

        Destroy(world, hero, t1);
        Assert.True(t3.IsShielded);
        Destroy(world, hero, t2);
        Assert.True(barracks.IsShielded);
        Destroy(world, hero, t3);
        Assert.All(t4s, t4 => Assert.True(t4.IsShielded));
        Destroy(world, hero, barracks);
        Assert.True(ancient.IsShielded);
        foreach (var t4 in t4s)
            Destroy(world, hero, t4);
        Destroy(world, hero, ancient);

        Assert.Equal(Team.Heights, world.Winner);
        Assert.Contains(world.DrainEvents(), e => e is GameOverEvent { Winner: Team.Heights });
        var tick = world.Tick;
        world.Step();
        Assert.Equal(tick, world.Tick);
    }

    [Fact]
    public void BackdoorProtectionHoldsUntilEnemyCreepsAreNear()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var t1 = Building(world, StructureKind.Tower, Lane.Mid, 1);
        var reduction = 0.06f * t1.PhysicalDefense / (1 + 0.06f * t1.PhysicalDefense);

        world.DealDamage(hero, t1, 100, DamageType.Physical);
        Assert.Equal(100 * (1 - reduction) * (1 - GameRules.BackdoorDamageReduction), t1.MaxHp - t1.Hp, 3);

        var damaged = t1.Hp;
        for (var i = 0; i < SimTime.TicksPerSecond; i++)
            world.Step();
        Assert.True(t1.Hp > damaged);

        world.AddUnit(UnitDefs.MeleeCreep, Team.Heights, t1.Pos + new GridPos(-3, -3));
        var before = t1.Hp;
        world.DealDamage(hero, t1, 100, DamageType.Physical);
        Assert.Equal(100 * (1 - reduction), before - t1.Hp, 3);
    }
}

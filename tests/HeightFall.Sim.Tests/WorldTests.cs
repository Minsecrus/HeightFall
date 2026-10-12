using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class WorldTests
{
    [Fact]
    public void WavesMarchAndFight()
    {
        var world = new World(StandardMap.Build());
        for (var i = 0; i < SimTime.Ticks(75); i++)
            world.Step();

        var creeps = world.Units.Where(u => u.IsCreep).ToList();
        Assert.Contains(creeps, c => c.Team == Team.Heights);
        Assert.Contains(creeps, c => c.Team == Team.Fall);
        // 3 波 × 3 路 × 2 方 × 5 个，已有小兵在河道交战中死亡
        Assert.True(creeps.Count < 90, $"alive creeps: {creeps.Count}");
    }

    [Fact]
    public void CollapseDropsEnemiesIntoPit()
    {
        var world = new World(StandardMap.Build());
        var caster = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var victim = world.SpawnHero(Heroes.Falcon, Team.Fall);
        world.Blink(caster, new GridPos(60, 40));
        world.Blink(victim, new GridPos(62, 40));
        Assert.True(world.LearnSkill(caster, 2));

        Assert.Null(world.OrderCast(caster, 2, victim.Pos));
        for (var i = 0; i < 12; i++)
            world.Step();

        Assert.True(victim.IsStunned);
        Assert.True(victim.Hp < victim.MaxHp);
        Assert.Equal(HeightRules.Ground - 2, world.Map.HeightAt(victim.Pos));
        Assert.Contains(world.DrainEvents(), e => e is FallEvent { Drop: 2 } fall && fall.Unit == victim);
    }

    [Fact]
    public void RecallReturnsToFountainUnlessInterruptedByDamage()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var enemy = world.SpawnHero(Heroes.Falcon, Team.Fall);
        world.Blink(hero, new GridPos(60, 40));
        world.Blink(enemy, new GridPos(90, 40));

        Assert.True(world.OrderRecall(hero));
        world.Step();
        world.DealDamage(enemy, hero, 10, DamageType.Physical);
        Assert.False(hero.IsRecalling);

        Assert.True(world.OrderRecall(hero));
        for (var i = 0; i <= SimTime.Ticks(GameRules.RecallSeconds); i++)
            world.Step();
        Assert.True(world.CanChangeSummoner(hero));
    }

    [Fact]
    public void WalkingOffCliffFallsButCannotWalkBackUp()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var top = new GridPos(30, 20);
        var below = new GridPos(31, 20);
        Assert.Equal(HeightRules.Plateau - HeightRules.Ground, world.Map.HeightAt(top) - world.Map.HeightAt(below));

        world.Blink(hero, top);
        world.OrderMove(hero, below);
        world.Step();
        while (hero.IsStepping)
            world.Step();
        world.Step();
        Assert.Equal(below, hero.Pos);
        Assert.True(hero.IsStunned);
        Assert.Contains(world.DrainEvents(), e => e is FallEvent { Drop: 2 });
        Assert.False(world.Map.CanStep(below, top));
    }
}

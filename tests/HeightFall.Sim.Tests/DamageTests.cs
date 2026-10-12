using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class DamageTests
{
    static float Reduction(float defense) => 0.06f * defense / (1 + 0.06f * MathF.Abs(defense));

    static ItemDef Item(string name) => Items.All.Single(item => item.Name == name);

    static (World World, Unit Caster, Unit Victim) Duel(UnitDef caster)
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(caster, Team.Heights);
        var victim = world.SpawnHero(Heroes.Bastion, Team.Fall);
        world.Blink(hero, new GridPos(60, 40));
        world.Blink(victim, new GridPos(62, 40));
        hero.Gold = victim.Gold = 2000;
        return (world, hero, victim);
    }

    /// <summary>直接结算技能的 1 级伤害效果，返回目标实际损失的生命，然后回满。</summary>
    static float SkillDamage(World world, Unit caster, Unit victim, int slot)
    {
        var skill = caster.Def.Skills[slot];
        var hp = victim.Hp;
        skill.Effects.OfType<DamageEffect>().Single().Apply(world, new SkillCast(caster, skill, 1, victim.Pos, [victim.Pos]));
        var dealt = hp - victim.Hp;
        victim.Hp = victim.MaxHp;
        return dealt;
    }

    [Fact]
    public void MagicSkillScalesWithMagicAttackAndIsReducedByMagicDefense()
    {
        var (world, caster, victim) = Duel(Heroes.Bastion);
        float Expected() => caster.MagicAttack * 1.6f * (1 - Reduction(victim.MagicDefense));
        var baseline = SkillDamage(world, caster, victim, 0);
        Assert.Equal(Expected(), baseline, 3);

        Assert.True(world.BuyItem(caster, Item("短剑")));
        Assert.Equal(baseline, SkillDamage(world, caster, victim, 0), 3);

        Assert.True(world.BuyItem(caster, Item("法杖")));
        var boosted = SkillDamage(world, caster, victim, 0);
        Assert.Equal(Expected(), boosted, 3);
        Assert.True(boosted > baseline);

        Assert.True(world.BuyItem(victim, Item("斗篷")));
        Assert.Equal(Expected(), SkillDamage(world, caster, victim, 0), 3);
        Assert.True(SkillDamage(world, caster, victim, 0) < boosted);
    }

    [Fact]
    public void PhysicalSkillScalesWithPhysicalAttackAndIsReducedByPhysicalDefense()
    {
        var (world, caster, victim) = Duel(Heroes.Falcon);
        float Expected() => caster.PhysicalAttack * 1.8f * (1 - Reduction(victim.PhysicalDefense));
        var baseline = SkillDamage(world, caster, victim, 0);
        Assert.Equal(Expected(), baseline, 3);

        Assert.True(world.BuyItem(caster, Item("法杖")));
        Assert.Equal(baseline, SkillDamage(world, caster, victim, 0), 3);

        Assert.True(world.BuyItem(caster, Item("短剑")));
        Assert.True(world.BuyItem(victim, Item("护盾")));
        Assert.Equal(Expected(), SkillDamage(world, caster, victim, 0), 3);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void FallStunsAndDealsPhysicalDamageDecidedByDropAlone(int drop)
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Falcon, Team.Heights);
        var top = new GridPos(30, 20);
        world.Blink(hero, top);
        world.DrainEvents();
        var hp = hero.Hp;

        // 没有施法者：地形无论被谁改变，坠落都只按落差结算。
        Assert.True(world.Map.AddHeightModifier(top, -drop, SimTime.Ticks(3)));

        Assert.True(hero.IsStunned);
        Assert.Equal(HeightRules.FallDamage(drop) * (1 - Reduction(hero.PhysicalDefense)), hp - hero.Hp, 3);
        var damage = Assert.Single(world.DrainEvents().OfType<DamageEvent>());
        Assert.Null(damage.Source);
        Assert.True(HeightRules.FallDamage(drop + 1) > HeightRules.FallDamage(drop));
    }

    [Fact]
    public void LethalFallWithoutEnemyHeroDamageRewardsNobody()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Falcon, Team.Heights);
        var enemy = world.SpawnHero(Heroes.Bastion, Team.Fall);
        var top = new GridPos(30, 20);
        world.Blink(hero, top);
        world.Blink(enemy, top + new GridPos(0, 3));
        var enemyGold = enemy.Gold;
        hero.Hp = 1;

        Assert.True(world.Map.AddHeightModifier(top, -2, SimTime.Ticks(3)));

        Assert.True(hero.IsDead);
        Assert.Equal(1, hero.Deaths);
        Assert.Equal(0, world.TeamKills(Team.Fall));
        Assert.Equal(0, enemy.Kills);
        Assert.Equal(enemyGold, enemy.Gold);
        Assert.Equal(0, enemy.Xp);
    }
}

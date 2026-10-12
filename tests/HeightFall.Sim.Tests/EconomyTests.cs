using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class EconomyTests
{
    [Fact]
    public void HeroesStartWithGoldButNoXp()
    {
        var hero = new World(StandardMap.Build()).SpawnHero(Heroes.Falcon, Team.Heights);

        Assert.Equal(GameRules.StartingGold, hero.Gold);
        Assert.Equal(0, hero.Xp);
        Assert.Equal(1, hero.Level);
    }

    [Fact]
    public void EveryGoldEarnedAlsoGrantsTheSameXp()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Falcon, Team.Heights);
        var creep = world.AddUnit(UnitDefs.MeleeCreep, Team.Fall, new GridPos(60, 40));

        world.DealDamage(hero, creep, creep.MaxHp * 10, DamageType.Physical);
        Assert.Equal(GameRules.StartingGold + UnitDefs.MeleeCreep.Gold, hero.Gold);
        Assert.Equal(UnitDefs.MeleeCreep.Gold, hero.Xp);

        var gold = hero.Gold;
        var xp = hero.Xp;
        for (var i = 0; i <= SimTime.Ticks(GameRules.PassiveGoldIntervalSeconds); i++)
            world.Step();
        Assert.Equal(gold + GameRules.PassiveGold, hero.Gold);
        Assert.Equal(xp + GameRules.PassiveGold, hero.Xp);

        var enemy = world.SpawnHero(Heroes.Bastion, Team.Fall);
        world.DealDamage(hero, enemy, enemy.MaxHp * 10, DamageType.Physical);
        Assert.Equal(2, hero.Level);
        Assert.Equal(xp + GameRules.PassiveGold + GameRules.HeroKillGold(1) - GameRules.XpToNext(1), hero.Xp);
    }

    [Fact]
    public void SellingRefundsGoldWithoutXp()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);

        Assert.True(world.BuyItem(hero, Items.All[0]));
        Assert.True(world.SellItem(hero, 0));

        Assert.Equal(GameRules.StartingGold - Items.All[0].Cost + World.ItemSellValue(Items.All[0]), hero.Gold);
        Assert.Equal(0, hero.Xp);
    }
}

using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class ShopTests
{
    [Fact]
    public void EmptyInventoryKeepsOriginalLevelScalingAndSpeedBuffs()
    {
        var world = new World(StandardMap.Build());
        foreach (var definition in new[] { Heroes.Bastion, Heroes.Falcon })
        {
            var hero = world.SpawnHero(definition, Team.Heights);
            hero.Level = 7;
            hero.StatScale = 1.3f;
            hero.SpeedBonus = 0.4f;
            hero.SpeedBonusTicks = 30;

            Assert.Empty(hero.Items);
            Assert.Equal(GameRules.StartingGold, hero.Gold);
            Assert.Equal((definition.MaxHp + definition.HpPerLevel * 6) * 1.3f, hero.MaxHp);
            Assert.Equal(definition.MaxMana + definition.ManaPerLevel * 6, hero.MaxMana);
            Assert.Equal((definition.PhysicalAttack + definition.PhysicalAttackPerLevel * 6) * 1.3f, hero.PhysicalAttack);
            Assert.Equal((definition.MagicAttack + definition.MagicAttackPerLevel * 6) * 1.3f, hero.MagicAttack);
            Assert.Equal(definition.PhysicalDefense + definition.PhysicalDefensePerLevel * 6, hero.PhysicalDefense);
            Assert.Equal(definition.MagicDefense + definition.MagicDefensePerLevel * 6, hero.MagicDefense);
            Assert.Equal(definition.MoveSpeed * 1.4f, hero.MoveSpeed);
        }
    }

    [Fact]
    public void EquipmentAppliesStatsAndPreservesResourceFractionsOnBuyAndSell()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        hero.Gold = 3000;
        hero.Hp = hero.MaxHp * 0.4f;
        hero.Mana = hero.MaxMana * 0.3f;
        hero.SpeedBonus = 0.5f;
        hero.SpeedBonusTicks = 30;
        var equipment = new[] { Item("短剑"), Item("生命石"), Item("能量石"), Item("护盾"), Item("轻靴") };

        foreach (var item in equipment)
        {
            Assert.Null(world.CheckBuy(hero, item));
            Assert.True(world.BuyItem(hero, item));
            Assert.Equal(0.4f, hero.Hp / hero.MaxHp, 5);
            Assert.Equal(0.3f, hero.Mana / hero.MaxMana, 5);
        }

        Assert.Equal(1850, hero.Gold);
        Assert.Equal(equipment, hero.Items);
        Assert.Equal(840, hero.MaxHp);
        Assert.Equal(420, hero.MaxMana);
        Assert.Equal(67, hero.PhysicalAttack);
        Assert.Equal(7, hero.PhysicalDefense);
        Assert.Equal(3.35f * 1.5f, hero.MoveSpeed);

        Assert.True(world.SellItem(hero, 1));
        Assert.Equal(1990, hero.Gold);
        Assert.Equal(680, hero.MaxHp);
        Assert.Equal(0.4f, hero.Hp / hero.MaxHp, 5);
        Assert.Equal(0.3f, hero.Mana / hero.MaxMana, 5);
        Assert.Equal(new[] { equipment[0], equipment[2], equipment[3], equipment[4] }, hero.Items);

        Assert.True(world.SellItem(hero, 1));
        Assert.Equal(2130, hero.Gold);
        Assert.Equal(300, hero.MaxMana);
        Assert.Equal(0.3f, hero.Mana / hero.MaxMana, 5);
    }

    [Theory]
    [InlineData("funds")]
    [InlineData("full")]
    [InlineData("foreign")]
    [InlineData("removed")]
    [InlineData("nonhero")]
    [InlineData("forged")]
    [InlineData("cloned")]
    [InlineData("null")]
    public void RejectedPurchaseIsAtomic(string scenario)
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        ItemDef item = Items.All[0];
        hero.Gold = 2000;
        switch (scenario)
        {
            case "funds": hero.Gold = item.Cost - 1; break;
            case "full":
                for (var i = 0; i < Unit.InventoryCapacity; i++)
                    Assert.True(world.BuyItem(hero, item));
                break;
            case "foreign":
                hero = new World(StandardMap.Build()).SpawnHero(Heroes.Bastion, Team.Heights);
                hero.Gold = 2000;
                break;
            case "removed": hero.IsRemoved = true; break;
            case "nonhero": hero = world.Units.First(u => u.IsStructure); hero.Gold = 2000; break;
            case "forged": item = new("短剑", "sword", -200, ItemCategory.Attack, PhysicalAttackBonus: 99999); break;
            case "cloned": item = item with { }; break;
            case "null": item = null!; break;
        }
        var before = Snapshot(hero);

        Assert.NotNull(world.CheckBuy(hero, item));
        Assert.False(world.BuyItem(hero, item));
        Assert.Equal(before, Snapshot(hero));
    }

    [Fact]
    public void DuplicateItemsStackAndSellingRemovesOnlyTheSelectedSlot()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        hero.Gold = 2000;
        for (var i = 0; i < Unit.InventoryCapacity; i++)
            Assert.True(world.BuyItem(hero, Items.All[0]));
        Assert.Equal(127, hero.PhysicalAttack);
        Assert.Equal(800, hero.Gold);

        Assert.True(world.SellItem(hero, 3));
        Assert.Equal(5, hero.Items.Count);
        Assert.All(hero.Items, item => Assert.Same(Items.All[0], item));
        Assert.Equal(115, hero.PhysicalAttack);
        Assert.Equal(940, hero.Gold);
        Assert.True(world.BuyItem(hero, Items.All[0]));
        Assert.Equal(740, hero.Gold);
        Assert.Equal(127, hero.PhysicalAttack);
    }

    [Fact]
    public void InvalidSalesLeaveStateUnchanged()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        hero.Gold = 1000;
        Assert.True(world.BuyItem(hero, Item("重甲")));
        var before = Snapshot(hero);
        foreach (var slot in new[] { -1, 1, Unit.InventoryCapacity, int.MaxValue })
        {
            Assert.False(world.SellItem(hero, slot));
            Assert.Equal(before, Snapshot(hero));
        }

        var foreignWorld = new World(StandardMap.Build());
        Assert.False(foreignWorld.SellItem(hero, 0));
        Assert.Equal(before, Snapshot(hero));
        hero.IsRemoved = true;
        Assert.False(world.SellItem(hero, 0));
        Assert.Equal(before, Snapshot(hero));
    }

    [Fact]
    public void DeadHeroCanShopWithoutRevivingAndRespawnsWithEquippedMaximums()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var enemy = world.SpawnHero(Heroes.Falcon, Team.Fall);
        hero.Gold = 1000;
        world.DealDamage(enemy, hero, hero.MaxHp * 10, DamageType.Physical);
        var respawnTicks = hero.RespawnTicks;

        Assert.True(world.BuyItem(hero, Item("生命石")));
        Assert.True(hero.IsDead);
        Assert.Equal(0, hero.Hp);
        Assert.Equal(respawnTicks, hero.RespawnTicks);
        Assert.True(world.SellItem(hero, 0));
        Assert.True(hero.IsDead);
        Assert.Equal(0, hero.Hp);
        Assert.Equal(respawnTicks, hero.RespawnTicks);
        Assert.True(world.BuyItem(hero, Item("生命石")));
        Assert.True(world.BuyItem(hero, Item("能量石")));

        for (var i = 0; i < respawnTicks; i++)
            world.Step();
        Assert.True(hero.IsAlive);
        Assert.Equal(840, hero.MaxHp);
        Assert.Equal(hero.MaxHp, hero.Hp);
        Assert.Equal(420, hero.MaxMana);
        Assert.Equal(hero.MaxMana, hero.Mana);
    }

    [Fact]
    public void InventoryAndCatalogCannotBeModifiedByConsumers()
    {
        var world = new World(StandardMap.Build());
        var hero = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var inventory = Assert.IsAssignableFrom<IList<ItemDef>>(hero.Items);
        Assert.Throws<NotSupportedException>(() => inventory.Add(Items.All[0]));
        var catalog = Assert.IsAssignableFrom<IList<ItemDef>>(Items.All);
        Assert.Throws<NotSupportedException>(() => catalog.Clear());
        Assert.Empty(hero.Items);
        Assert.Equal(14, Items.All.Count);
    }

    static ItemDef Item(string name) => Items.All.Single(item => item.Name == name);

    static string Snapshot(Unit hero) => string.Join("|", hero.Gold, hero.Hp, hero.Mana,
        hero.MaxHp, hero.MaxMana, hero.PhysicalAttack, hero.MagicAttack, hero.PhysicalDefense, hero.MagicDefense, hero.MoveSpeed,
        string.Join(",", hero.Items.Select(item => item.Name)));
}

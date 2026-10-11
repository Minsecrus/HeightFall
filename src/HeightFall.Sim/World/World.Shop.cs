namespace HeightFall.Sim;

public sealed partial class World
{
    /// <summary>本局英雄可随时购买，死亡时也能整理装备。</summary>
    public string? CheckBuy(Unit hero, ItemDef item)
    {
        if (!CanUseShop(hero))
            return "当前英雄无法购买";
        if (item is null || !Items.All.Any(stock => ReferenceEquals(stock, item)))
            return "装备不在商店中";
        if (hero.Items.Count >= Unit.InventoryCapacity)
            return "装备栏已满";
        if (hero.Gold < item.Cost)
            return "金币不足";
        return null;
    }

    public bool BuyItem(Unit hero, ItemDef item)
    {
        if (CheckBuy(hero, item) is not null)
            return false;

        var hpFraction = HealthFraction(hero);
        var manaFraction = ManaFraction(hero);
        hero.Gold -= item.Cost;
        hero.ItemValues.Add(item);
        RestoreResourceFractions(hero, hpFraction, manaFraction);
        return true;
    }

    public bool SellItem(Unit hero, int slot)
    {
        if (!CanUseShop(hero) || slot < 0 || slot >= hero.Items.Count)
            return false;

        var hpFraction = HealthFraction(hero);
        var manaFraction = ManaFraction(hero);
        var item = hero.ItemValues[slot];
        hero.ItemValues.RemoveAt(slot);
        hero.Gold += ItemSellValue(item);
        RestoreResourceFractions(hero, hpFraction, manaFraction);
        return true;
    }

    public static int ItemSellValue(ItemDef item) => item.Cost * 7 / 10;

    bool CanUseShop(Unit hero) => hero is { IsHero: true, IsRemoved: false } && _units.Contains(hero);

    static float HealthFraction(Unit hero) => hero.IsDead || hero.MaxHp <= 0
        ? 0 : Math.Clamp(hero.Hp / hero.MaxHp, 0, 1);

    static float ManaFraction(Unit hero) => hero.MaxMana <= 0
        ? 0 : Math.Clamp(hero.Mana / hero.MaxMana, 0, 1);

    static void RestoreResourceFractions(Unit hero, float hpFraction, float manaFraction)
    {
        hero.Hp = hpFraction * hero.MaxHp;
        hero.Mana = manaFraction * hero.MaxMana;
    }
}

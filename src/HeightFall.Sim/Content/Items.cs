namespace HeightFall.Sim;

public enum ItemCategory { Attack, Defense, Movement }

/// <summary>装备提供固定属性；图标由共享 Godot UI 按 token 绘制。</summary>
public sealed record ItemDef(
    string Name,
    string Icon,
    int Cost,
    ItemCategory Category,
    float HpBonus = 0,
    float ManaBonus = 0,
    float DamageBonus = 0,
    float ArmorBonus = 0,
    float MoveSpeedBonus = 0);

public static class Items
{
    public static IReadOnlyList<ItemDef> All { get; } = Array.AsReadOnly<ItemDef>(
    [
        new("短剑", "sword", 200, ItemCategory.Attack, DamageBonus: 12),
        new("战斧", "axe", 450, ItemCategory.Attack, HpBonus: 100, DamageBonus: 22),
        new("长矛", "spear", 600, ItemCategory.Attack, DamageBonus: 35),
        new("生命石", "ruby", 200, ItemCategory.Defense, HpBonus: 160),
        new("能量石", "sapphire", 200, ItemCategory.Defense, ManaBonus: 120),
        new("护盾", "shield", 300, ItemCategory.Defense, ArmorBonus: 4),
        new("重甲", "armor", 600, ItemCategory.Defense, HpBonus: 320, ArmorBonus: 3),
        new("轻靴", "boots", 250, ItemCategory.Movement, MoveSpeedBonus: 0.35f),
        new("疾行靴", "boots", 550, ItemCategory.Movement, MoveSpeedBonus: 0.6f),
    ]);
}

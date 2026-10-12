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
    float PhysicalAttackBonus = 0,
    float MagicAttackBonus = 0,
    float PhysicalDefenseBonus = 0,
    float MagicDefenseBonus = 0,
    float MoveSpeedBonus = 0);

public static class Items
{
    public static IReadOnlyList<ItemDef> All { get; } = Array.AsReadOnly<ItemDef>(
    [
        new("短剑", "sword", 200, ItemCategory.Attack, PhysicalAttackBonus: 12),
        new("战斧", "axe", 450, ItemCategory.Attack, HpBonus: 100, PhysicalAttackBonus: 22),
        new("长矛", "spear", 600, ItemCategory.Attack, PhysicalAttackBonus: 35),
        new("法杖", "staff", 200, ItemCategory.Attack, MagicAttackBonus: 12),
        new("秘典", "tome", 450, ItemCategory.Attack, ManaBonus: 150, MagicAttackBonus: 22),
        new("法珠", "orb", 600, ItemCategory.Attack, MagicAttackBonus: 35),
        new("生命石", "ruby", 200, ItemCategory.Defense, HpBonus: 160),
        new("能量石", "sapphire", 200, ItemCategory.Defense, ManaBonus: 120),
        new("护盾", "shield", 300, ItemCategory.Defense, PhysicalDefenseBonus: 4),
        new("重甲", "armor", 600, ItemCategory.Defense, HpBonus: 320, PhysicalDefenseBonus: 3),
        new("斗篷", "cloak", 300, ItemCategory.Defense, MagicDefenseBonus: 4),
        new("法衣", "robe", 600, ItemCategory.Defense, HpBonus: 320, MagicDefenseBonus: 3),
        new("轻靴", "boots", 250, ItemCategory.Movement, MoveSpeedBonus: 0.35f),
        new("疾行靴", "boots", 550, ItemCategory.Movement, MoveSpeedBonus: 0.6f),
    ]);
}

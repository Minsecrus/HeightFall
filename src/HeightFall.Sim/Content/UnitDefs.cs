namespace HeightFall.Sim;

/// <summary>小兵和建筑的数值表。</summary>
public static class UnitDefs
{
    public static readonly UnitDef MeleeCreep = new()
    {
        Name = "近战兵", Kind = UnitKind.MeleeCreep,
        MaxHp = 420, Damage = 15, AttackRange = 1.5f, AttackInterval = 1f,
        Armor = 2, MoveSpeed = 3f, Gold = 38, Xp = 57,
    };

    public static readonly UnitDef MidCreep = new()
    {
        Name = "中程兵", Kind = UnitKind.MidCreep,
        MaxHp = 320, Damage = 16, AttackRange = 3f, AttackInterval = 1f, ProjectileSpeed = 10f,
        Armor = 1, MoveSpeed = 3f, Gold = 42, Xp = 62,
    };

    public static readonly UnitDef RangedCreep = new()
    {
        Name = "远程兵", Kind = UnitKind.RangedCreep,
        MaxHp = 240, Damage = 17, AttackRange = 5f, AttackInterval = 1f, ProjectileSpeed = 9f,
        MoveSpeed = 3f, Gold = 48, Xp = 69,
    };

    public static readonly UnitDef SiegeCreep = new()
    {
        Name = "炮车兵", Kind = UnitKind.SiegeCreep,
        MaxHp = 650, Damage = 30, AttackRange = 7f, AttackInterval = 2.5f, ProjectileSpeed = 8f,
        MoveSpeed = 3f, Gold = 70, Xp = 88,
    };

    public static readonly UnitDef SuperCreep = new()
    {
        Name = "超级兵", Kind = UnitKind.SuperCreep,
        MaxHp = 950, Damage = 42, AttackRange = 1.5f, AttackInterval = 1f,
        Armor = 3, MoveSpeed = 3f, Gold = 25, Xp = 25,
    };

    static readonly UnitDef[] Towers =
    [
        Tower("一塔", 1800, 100),
        Tower("二塔", 2100, 120),
        Tower("三塔", 2400, 140),
        Tower("四塔", 2600, 140),
    ];

    public static readonly UnitDef Barracks = new()
    {
        Name = "兵营", Kind = UnitKind.Barracks, Size = 3, MaxHp = 3000, Armor = 10,
    };

    public static readonly UnitDef Ancient = new()
    {
        Name = "基地核心", Kind = UnitKind.Ancient, Size = 4, MaxHp = 4500, Armor = 13,
    };

    public static readonly UnitDef Fountain = new()
    {
        Name = "泉水", Kind = UnitKind.Fountain, Size = 5, BlocksTiles = false, Invulnerable = true,
        MaxHp = 1, Damage = 180, AttackRange = 12, AttackInterval = 0.6f, ProjectileSpeed = 16f,
    };

    public static UnitDef ForStructure(Structure s) => s.Kind switch
    {
        StructureKind.Tower => Towers[s.Tier - 1],
        StructureKind.Barracks => Barracks,
        StructureKind.Ancient => Ancient,
        _ => Fountain,
    };

    static UnitDef Tower(string name, float hp, float damage) => new()
    {
        Name = name, Kind = UnitKind.Tower, Size = 2,
        MaxHp = hp, Damage = damage, AttackRange = 7, AttackInterval = 1f, ProjectileSpeed = 12f, Armor = 12,
    };
}

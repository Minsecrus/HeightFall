namespace HeightFall.Sim;

/// <summary>小兵和建筑的数值表。高度以级为单位（1 级 = 0.2 m），与模型一致。</summary>
public static class UnitDefs
{
    public static readonly UnitDef MeleeCreep = new()
    {
        Name = "近战兵", Kind = UnitKind.MeleeCreep, BodyHeight = 3.5f,
        MaxHp = 420, PhysicalAttack = 15, AttackRange = 1.5f, AttackInterval = 1f,
        PhysicalDefense = 2, MoveSpeed = 3f, Gold = 38,
    };

    public static readonly UnitDef MidCreep = new()
    {
        Name = "中程兵", Kind = UnitKind.MidCreep, BodyHeight = 4f,
        MaxHp = 320, PhysicalAttack = 16, AttackRange = 3f, AttackInterval = 1f, ProjectileSpeed = 10f, LaunchHeight = 2f,
        PhysicalDefense = 1, MoveSpeed = 3f, Gold = 42,
    };

    public static readonly UnitDef RangedCreep = new()
    {
        Name = "远程兵", Kind = UnitKind.RangedCreep, BodyHeight = 4f,
        MaxHp = 240, PhysicalAttack = 17, AttackRange = 5f, AttackInterval = 1f, ProjectileSpeed = 9f, LaunchHeight = 2f,
        MoveSpeed = 3f, Gold = 48,
    };

    public static readonly UnitDef SiegeCreep = new()
    {
        Name = "炮车兵", Kind = UnitKind.SiegeCreep, BodyHeight = 4.5f,
        MaxHp = 650, PhysicalAttack = 30, AttackRange = 7f, AttackInterval = 2.5f, ProjectileSpeed = 8f, LaunchHeight = 2.25f,
        ArcingProjectile = true,
        MoveSpeed = 3f, Gold = 70,
    };

    public static readonly UnitDef SuperCreep = new()
    {
        Name = "超级兵", Kind = UnitKind.SuperCreep, BodyHeight = 5.5f,
        MaxHp = 950, PhysicalAttack = 42, AttackRange = 1.5f, AttackInterval = 1f,
        PhysicalDefense = 3, MoveSpeed = 3f, Gold = 25,
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
        Name = "兵营", Kind = UnitKind.Barracks, Size = 3, BodyHeight = 8.25f, MaxHp = 3000, PhysicalDefense = 10,
    };

    public static readonly UnitDef Ancient = new()
    {
        Name = "基地核心", Kind = UnitKind.Ancient, Size = 4, BodyHeight = 19.5f, MaxHp = 4500, PhysicalDefense = 13,
    };

    /// <summary>泉水没有模型，攻击从 2 m 高处发出。</summary>
    public static readonly UnitDef Fountain = new()
    {
        Name = "泉水", Kind = UnitKind.Fountain, Size = 5, BlocksTiles = false, Invulnerable = true,
        MaxHp = 1, PhysicalAttack = 180, AttackRange = 12, AttackInterval = 0.6f, ProjectileSpeed = 16f, LaunchHeight = 10f,
    };

    public static UnitDef ForStructure(Structure s) => s.Kind switch
    {
        StructureKind.Tower => Towers[s.Tier - 1],
        StructureKind.Barracks => Barracks,
        StructureKind.Ancient => Ancient,
        _ => Fountain,
    };

    /// <summary>防御塔高 3.2 m，从塔顶悬浮的发光体（2.75 m）出手。</summary>
    static UnitDef Tower(string name, float hp, float attack) => new()
    {
        Name = name, Kind = UnitKind.Tower, Size = 2, BodyHeight = 16f,
        MaxHp = hp, PhysicalAttack = attack, AttackRange = 7, AttackInterval = 1f, ProjectileSpeed = 12f, LaunchHeight = 13.75f,
        PhysicalDefense = 12,
    };
}

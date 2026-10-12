namespace HeightFall.Sim;

public enum UnitKind
{
    Hero,
    MeleeCreep,
    MidCreep,
    RangedCreep,
    SiegeCreep,
    SuperCreep,
    Tower,
    Barracks,
    Ancient,
    Fountain,
}

public enum DamageType
{
    /// <summary>按物攻计算，受物防减免。坠落伤害也是物理伤害。</summary>
    Physical,

    /// <summary>按法攻计算，受法防减免。</summary>
    Magic,
}

public static class UnitKindExtensions
{
    public static bool IsStructure(this UnitKind kind) =>
        kind is UnitKind.Tower or UnitKind.Barracks or UnitKind.Ancient or UnitKind.Fountain;

    public static bool IsCreep(this UnitKind kind) =>
        kind is UnitKind.MeleeCreep or UnitKind.MidCreep or UnitKind.RangedCreep
            or UnitKind.SiegeCreep or UnitKind.SuperCreep;
}

/// <summary>单位的基础数值。射程、移速等距离都以格为单位。</summary>
public sealed record UnitDef
{
    public required string Name { get; init; }
    public required UnitKind Kind { get; init; }

    /// <summary>渲染层用来选择模型。</summary>
    public string Model { get; init; } = "";

    public int Size { get; init; } = 1;
    public bool BlocksTiles { get; init; } = true;
    public bool Invulnerable { get; init; }

    /// <summary>模型高度（级，1 级 = 0.2 m）。投射物瞄准模型的一半高度。</summary>
    public float BodyHeight { get; init; }

    public float MaxHp { get; init; }
    public float HpPerLevel { get; init; }
    public float HpRegen { get; init; }
    public float MaxMana { get; init; }
    public float ManaPerLevel { get; init; }
    public float ManaRegen { get; init; }
    public float ManaRegenPerLevel { get; init; }

    /// <summary>物攻：普攻伤害，也是物理技能伤害百分比的基数。</summary>
    public float PhysicalAttack { get; init; }
    public float PhysicalAttackPerLevel { get; init; }

    /// <summary>法攻：法术技能伤害百分比的基数。</summary>
    public float MagicAttack { get; init; }
    public float MagicAttackPerLevel { get; init; }

    public float AttackRange { get; init; }
    public float AttackInterval { get; init; } = 1f;

    /// <summary>投射物速度（格/秒）；为 0 时是近战，攻击立即生效。</summary>
    public float ProjectileSpeed { get; init; }

    /// <summary>投射物出手点比单位所在格高出的级数。</summary>
    public float LaunchHeight { get; init; }

    /// <summary>投射物走抛物线（可以越过地形）；否则走直线，会被路径上更高的地形挡住。</summary>
    public bool ArcingProjectile { get; init; }

    /// <summary>物防和法防按同一公式减免对应类型的伤害。</summary>
    public float PhysicalDefense { get; init; }
    public float PhysicalDefensePerLevel { get; init; }
    public float MagicDefense { get; init; }
    public float MagicDefensePerLevel { get; init; }

    public float MoveSpeed { get; init; }

    /// <summary>被击杀时给予击杀者的金币；英雄获得金币时同时获得等量经验。</summary>
    public int Gold { get; init; }

    public IReadOnlyList<SkillDef> Skills { get; init; } = [];
}

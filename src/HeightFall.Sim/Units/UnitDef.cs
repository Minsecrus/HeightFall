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
    /// <summary>受护甲减免。</summary>
    Physical,

    /// <summary>受魔抗减免。</summary>
    Magic,

    Pure,
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

    public float MaxHp { get; init; }
    public float HpPerLevel { get; init; }
    public float HpRegen { get; init; }
    public float MaxMana { get; init; }
    public float ManaPerLevel { get; init; }
    public float ManaRegen { get; init; }
    public float ManaRegenPerLevel { get; init; }

    public float Damage { get; init; }
    public float DamagePerLevel { get; init; }
    public float AttackRange { get; init; }
    public float AttackInterval { get; init; } = 1f;

    /// <summary>投射物速度（格/秒）；为 0 时是近战，攻击立即生效。</summary>
    public float ProjectileSpeed { get; init; }

    public float Armor { get; init; }
    public float ArmorPerLevel { get; init; }

    /// <summary>魔法伤害减免比例，0–1。</summary>
    public float MagicResist { get; init; }

    public float MoveSpeed { get; init; }

    /// <summary>被击杀时给予的金钱和经验。</summary>
    public int Gold { get; init; }
    public int Xp { get; init; }

    public IReadOnlyList<SkillDef> Skills { get; init; } = [];
}

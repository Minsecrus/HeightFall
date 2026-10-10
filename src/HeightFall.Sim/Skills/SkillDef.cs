namespace HeightFall.Sim;

public enum SkillTargeting
{
    /// <summary>按键立即释放。</summary>
    None,

    /// <summary>朝鼠标方向释放，方向取最近的 8 方向之一。</summary>
    Direction,

    /// <summary>对射程内的一个格子释放。</summary>
    Tile,
}

/// <summary>技能 = 目标选择器（选出哪些格子）+ 按顺序执行的效果列表。</summary>
public sealed record SkillDef
{
    public required string Name { get; init; }

    /// <summary>渲染层用来选择图标。</summary>
    public string Icon { get; init; } = "";

    public required SkillTargeting Targeting { get; init; }

    /// <summary>施法距离（格），只对 Tile 类技能有效。</summary>
    public float Range { get; init; }

    /// <summary>目标格最多比施法者所在格高几级；为空时不限制。用于位移技能的落点。</summary>
    public int? MaxRise { get; init; }

    public required TileSelector Area { get; init; }
    public required IReadOnlyList<SkillEffect> Effects { get; init; }

    /// <summary>按技能等级取值，数组短于等级时取最后一项。</summary>
    public required float[] Cooldown { get; init; }

    public required float[] ManaCost { get; init; }
    public float CastPoint { get; init; } = 0.3f;
    public bool IsUltimate { get; init; }

    public int MaxLevel => IsUltimate ? GameRules.UltimateLevels.Length : 4;
}

/// <summary>一次施法的上下文，效果按顺序作用在其上。</summary>
public sealed record SkillCast(Unit Caster, SkillDef Skill, int Level, GridPos Target, IReadOnlyList<GridPos> Tiles);

public static class PerLevel
{
    public static float At(this float[] values, int level) => values[Math.Clamp(level, 1, values.Length) - 1];
}

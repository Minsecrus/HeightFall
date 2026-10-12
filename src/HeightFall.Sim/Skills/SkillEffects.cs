namespace HeightFall.Sim;

public abstract record SkillEffect
{
    internal abstract void Apply(World world, SkillCast cast);
}

/// <summary>效果作用的对象。</summary>
public enum EffectTargets
{
    /// <summary>范围内的敌方英雄和小兵（不含建筑和空中单位）。</summary>
    Enemies,

    /// <summary>范围内的敌方小兵。</summary>
    EnemyCreeps,

    /// <summary>范围内的己方英雄（含施法者）。</summary>
    AllyHeroes,

    Caster,
}

/// <summary>对范围内的单位造成伤害：施法者对应攻击（物理按物攻、法术按法攻）的百分比，随技能等级变化。</summary>
public sealed record DamageEffect(float[] Percent, DamageType Type, EffectTargets Targets = EffectTargets.Enemies) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
        var amount = cast.Caster.Attack(Type) * Percent.At(cast.Level) / 100f;
        foreach (var unit in world.UnitsAffected(cast, Targets))
            world.DealDamage(cast.Caster, unit, amount, Type, fromSkill: true);
    }
}

public sealed record StunEffect(float[] Seconds, EffectTargets Targets = EffectTargets.Enemies) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
        foreach (var unit in world.UnitsAffected(cast, Targets))
            world.Stun(unit, Seconds.At(cast.Level));
    }
}

/// <summary>回复最大生命的一定比例。</summary>
public sealed record HealEffect(float Fraction, EffectTargets Targets) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
        foreach (var unit in world.UnitsAffected(cast, Targets))
            unit.Hp = MathF.Min(unit.MaxHp, unit.Hp + unit.MaxHp * Fraction);
    }
}

/// <summary>解除眩晕，并在一段时间内免疫眩晕。</summary>
public sealed record CleanseEffect(float ImmuneSeconds) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
        cast.Caster.StunTicks = 0;
        cast.Caster.StunImmuneTicks = SimTime.Ticks(ImmuneSeconds);
    }
}

/// <summary>施法者移速提高一定比例。</summary>
public sealed record SpeedBoostEffect(float Bonus, float Seconds) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
        cast.Caster.SpeedBonus = Bonus;
        cast.Caster.SpeedBonusTicks = SimTime.Ticks(Seconds);
    }
}

/// <summary>范围内的格子临时升高或降低。脚下格子一次下降 2 级及以上的单位会坠落。</summary>
public sealed record ShiftHeightEffect(int Delta, float Seconds) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
        foreach (var tile in cast.Tiles)
            world.Map.AddHeightModifier(tile, Delta, SimTime.Ticks(Seconds));
    }
}

/// <summary>地面瞬移：只检查落点，不受中间地形影响；落点高度受技能的 MaxRise 限制，落差 ≥ 2 级时坠落。</summary>
public sealed record BlinkEffect : SkillEffect
{
    internal override void Apply(World world, SkillCast cast) =>
        world.Blink(cast.Caster, cast.Target, cast.Skill.MaxRise ?? int.MaxValue, fallOnDrop: true);
}

/// <summary>空中位移：飞行期间不受地形限制，也不会被选为目标；落点高度受技能的 MaxRise 限制，落地不算坠落。</summary>
public sealed record LeapEffect(float Seconds) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast) =>
        world.Leap(cast.Caster, cast.Target, SimTime.Ticks(Seconds), cast.Skill.MaxRise ?? int.MaxValue);
}

/// <summary>之后的效果延迟生效；等待期间双方都能看到落点。</summary>
public sealed record DelayEffect(float Seconds) : SkillEffect
{
    internal override void Apply(World world, SkillCast cast)
    {
    }
}

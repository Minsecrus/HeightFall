namespace HeightFall.Sim;

public enum CastError
{
    NotLearned,
    Cooldown,
    Mana,
    Dead,

    /// <summary>目标格超出技能允许的落点高度，或不在地图内。</summary>
    InvalidTarget,
}

public enum LearnError
{
    NoPoints,
    MaxLevel,

    /// <summary>大招等级受英雄等级限制，见 GameRules.UltimateLevels。</summary>
    HeroLevel,
}

public sealed partial class World
{
    /// <summary>技能当前能否释放；可以时返回 null。</summary>
    public CastError? CheckCast(Unit u, int slot)
    {
        if (!u.IsAlive)
            return CastError.Dead;
        if (slot >= u.SkillCount)
            return CastError.NotLearned;
        var level = u.SkillLevels[slot];
        if (level == 0)
            return CastError.NotLearned;
        if (u.SkillCooldowns[slot] > 0)
            return CastError.Cooldown;
        if (u.Mana < u.SkillAt(slot).ManaCost.At(level))
            return CastError.Mana;
        return null;
    }

    /// <summary>目标格是否合法：在地图内，且不超过技能的 MaxRise（比施法者当前所在格高出的级数）。</summary>
    public bool CanTarget(Unit u, int slot, GridPos target) =>
        Map.InBounds(target)
        && (u.SkillAt(slot).MaxRise is not { } rise || Map.HeightAt(target) - Map.HeightAt(u.Pos) <= rise);

    public CastError? OrderCast(Unit u, int slot, GridPos target)
    {
        if (CheckCast(u, slot) is { } error)
            return error;
        if (!CanTarget(u, slot, target))
            return CastError.InvalidTarget;
        CancelCast(u);
        u.RecallTicks = 0;
        u.Order = new CastOrder(slot, target);
        u.AttackTarget = null;
        ClearPath(u);
        return null;
    }

    /// <summary>技能当前能否学习或升级；可以时返回 null。召唤师技能固定 1 级，不能升级。</summary>
    public LearnError? CheckLearn(Unit hero, int slot)
    {
        if (slot >= hero.Def.Skills.Count)
            return LearnError.MaxLevel;
        var skill = hero.SkillAt(slot);
        var level = hero.SkillLevels[slot];
        if (level >= skill.MaxLevel)
            return LearnError.MaxLevel;
        if (skill.IsUltimate && hero.Level < GameRules.UltimateLevels[level])
            return LearnError.HeroLevel;
        if (hero.SkillPoints <= 0)
            return LearnError.NoPoints;
        return null;
    }

    public bool CanLearn(Unit hero, int slot) => CheckLearn(hero, slot) is null;

    public bool LearnSkill(Unit hero, int slot)
    {
        if (!CanLearn(hero, slot))
            return false;
        hero.SkillLevelValues[slot]++;
        hero.SkillPoints--;
        return true;
    }

    /// <summary>技能会影响的格子，用于施法前的预览。</summary>
    public IReadOnlyList<GridPos> PreviewSkill(Unit caster, int slot, GridPos target) =>
        caster.SkillAt(slot).Area.Select(Map, caster.Pos, target);

    /// <summary>调试用：直接升一级。</summary>
    public void DebugLevelUp(Unit hero) => AddXp(hero, GameRules.XpToNext(hero.Level) - hero.Xp);

    /// <summary>调试用：把单位瞬移到某格附近。</summary>
    public void DebugTeleport(Unit u, GridPos tile) => Blink(u, tile);

    void CancelCast(Unit u)
    {
        if (u.IsCasting)
            u.CastTicksLeft = -1;
    }

    /// <summary>目标格超出施法距离时先走过去，进入距离后开始前摇。</summary>
    void UpdateCastOrder(Unit hero, CastOrder cast)
    {
        var skill = hero.SkillAt(cast.Slot);
        if (CheckCast(hero, cast.Slot) is not null)
        {
            hero.Order = null;
            return;
        }
        if (skill.Targeting == SkillTargeting.Tile && DistanceToTile(hero, cast.Target) > skill.Range)
        {
            if (MoveToward(hero, cast.Target, skill.Range) == MoveResult.Stuck)
                hero.Order = null;
            return;
        }
        ClearPath(hero);
        hero.CastTicksLeft = SimTime.Ticks(skill.CastPoint);
    }

    void UpdateCasting(Unit hero)
    {
        if (--hero.CastTicksLeft > 0)
            return;
        hero.CastTicksLeft = -1;
        if (hero.Order is not CastOrder cast)
            return;
        hero.Order = null;
        // 走过去的途中自己所在的高度可能变了，生效前再检查一次落点
        if (CheckCast(hero, cast.Slot) is not null || !CanTarget(hero, cast.Slot, cast.Target))
            return;

        var skill = hero.SkillAt(cast.Slot);
        var level = hero.SkillLevels[cast.Slot];
        hero.Mana -= skill.ManaCost.At(level);
        hero.CooldownValues[cast.Slot] = SimTime.Ticks(skill.Cooldown.At(level));
        var tiles = skill.Area.Select(Map, hero.Pos, cast.Target);
        RunEffects(new SkillCast(hero, skill, level, cast.Target, tiles), 0);
    }

    /// <summary>按顺序执行效果；遇到延迟时显示落点，把剩下的效果排到之后执行。</summary>
    void RunEffects(SkillCast cast, int start)
    {
        var effects = cast.Skill.Effects;
        for (var i = start; i < effects.Count; i++)
        {
            if (effects[i] is DelayEffect delay)
            {
                var ticks = SimTime.Ticks(delay.Seconds);
                var next = i + 1;
                _telegraphs.Add(new Telegraph(cast.Caster.Team, cast.Tiles, Tick + ticks));
                Schedule(ticks, () => RunEffects(cast, next));
                return;
            }
            Emit(new EffectEvent(cast, effects[i], cast.Caster.Pos));
            effects[i].Apply(this, cast);
        }
    }

    /// <summary>
    /// 瞬移到目标格附近：落点最多比原地高 maxRise 级；fallOnDrop 时，落差 ≥ 2 级与走下高崖一样坠落。
    /// </summary>
    internal void Blink(Unit u, GridPos target, int maxRise = int.MaxValue, bool fallOnDrop = false)
    {
        var fromHeight = Map.HeightAt(u.Pos);
        if (FindStandableNear(target, 2, p => Map.HeightAt(p) - fromHeight <= maxRise) is not { } landing)
            return;
        Vacate(u);
        u.Pos = u.PrevPos = landing;
        u.StepTicksLeft = 0;
        u.PendingFall = 0;
        Occupy(u);
        ClearPath(u);
        var drop = fromHeight - Map.HeightAt(landing);
        if (fallOnDrop && drop >= HeightRules.FallThreshold)
            Fall(u, drop);
    }

    /// <summary>起跳时就占住落点（最多比原地高 maxRise 级），飞行期间不受地形限制，落地不算坠落。</summary>
    internal void Leap(Unit u, GridPos target, int ticks, int maxRise = int.MaxValue)
    {
        var fromHeight = Map.HeightAt(u.Pos);
        if (FindStandableNear(target, 2, p => Map.HeightAt(p) - fromHeight <= maxRise) is not { } landing)
            return;
        Vacate(u);
        u.AirborneFrom = u.Pos;
        u.Pos = u.PrevPos = landing;
        u.StepTicksLeft = 0;
        u.PendingFall = 0;
        u.AirborneTicks = u.AirborneTotal = Math.Max(1, ticks);
        Occupy(u);
        ClearPath(u);
    }
}

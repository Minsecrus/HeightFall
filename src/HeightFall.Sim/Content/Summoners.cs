namespace HeightFall.Sim;

/// <summary>召唤师技能：开局前自选一个，占用英雄的第 5 个技能栏位，固定 1 级、不耗蓝。</summary>
public static class Summoners
{
    public static readonly SkillDef Flash = new()
    {
        Name = "闪现", Icon = "blink", Targeting = SkillTargeting.Tile, Range = 4, MaxRise = HeightRules.MaxStep,
        Area = new TargetTileSelector(), Effects = [new BlinkEffect()],
        Cooldown = [120], ManaCost = [0], CastPoint = 0f,
    };

    public static readonly SkillDef Heal = new()
    {
        Name = "治疗", Icon = "heal", Targeting = SkillTargeting.None,
        Area = new CircleSelector(4), Effects = [new HealEffect(0.15f, EffectTargets.AllyHeroes)],
        Cooldown = [120], ManaCost = [0], CastPoint = 0f,
    };

    public static readonly SkillDef Purify = new()
    {
        Name = "净化", Icon = "purify", Targeting = SkillTargeting.None,
        Area = new TargetTileSelector(), Effects = [new CleanseEffect(1.5f)],
        Cooldown = [120], ManaCost = [0], CastPoint = 0f,
    };

    public static readonly SkillDef Sprint = new()
    {
        Name = "疾跑", Icon = "sprint", Targeting = SkillTargeting.None,
        Area = new TargetTileSelector(), Effects = [new SpeedBoostEffect(0.3f, 10)],
        Cooldown = [75], ManaCost = [0], CastPoint = 0f,
    };

    public static readonly SkillDef Smite = new()
    {
        Name = "惩击", Icon = "smite", Targeting = SkillTargeting.Tile, Range = 4,
        Area = new CircleSelector(1),
        Effects =
        [
            new DamageEffect([1000], DamageType.Physical, EffectTargets.EnemyCreeps),
            new StunEffect([1f], EffectTargets.EnemyCreeps),
        ],
        Cooldown = [30], ManaCost = [0], CastPoint = 0f,
    };

    public static readonly SkillDef Daze = new()
    {
        Name = "晕眩", Icon = "daze", Targeting = SkillTargeting.None,
        Area = new CircleSelector(2.5f), Effects = [new StunEffect([0.75f])],
        Cooldown = [90], ManaCost = [0], CastPoint = 0f,
    };

    public static readonly IReadOnlyList<SkillDef> All = [Flash, Heal, Purify, Sprint, Smite, Daze];
}

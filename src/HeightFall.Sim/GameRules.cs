namespace HeightFall.Sim;

/// <summary>对局规则中的固定数值。</summary>
public static class GameRules
{
    public const int MaxHeroLevel = 25;

    /// <summary>大招第 1、2、3 级分别需要的英雄等级。</summary>
    public static readonly int[] UltimateLevels = [6, 12, 18];

    public const float XpShareRadius = 12f;
    public const float DenyHpFraction = 0.5f;
    public const float DenyXpFactor = 0.5f;
    public const int StructureGold = 100;
    public const float PassiveGoldIntervalSeconds = 2f;
    public const int PassiveGold = 3;

    public const float FountainHealPerSecond = 0.05f;

    /// <summary>回城读条时间；受到伤害、被眩晕或自己行动都会打断，没有冷却。</summary>
    public const float RecallSeconds = 6f;

    /// <summary>恢复：持续时间内每秒回复最大生命和法力的比例，受到伤害即中断。</summary>
    public const float RestoreSeconds = 5f;
    public const float RestorePerSecond = 0.04f;
    public const float RestoreCooldownSeconds = 60f;

    /// <summary>英雄攻击敌方英雄后，这么长时间内会吸引附近防御塔和小兵的火力。</summary>
    public const float HeroAggroSeconds = 2f;

    public const float WaveIntervalSeconds = 30f;
    public const float SiegeFromSeconds = 240f;
    public const float CreepUpgradeSeconds = 450f;
    public const float CreepUpgradeStep = 0.1f;
    public const float CreepAggroRange = 5f;
    public const float CreepLeashRange = 8f;

    public static int XpToNext(int level) => 200 + 120 * (level - 1);

    public static float RespawnSeconds(int level) => 4 + 2 * level;

    public static int HeroKillGold(int level) => 200 + 10 * level;

    public static int HeroKillXp(int level) => 100 + 25 * level;
}

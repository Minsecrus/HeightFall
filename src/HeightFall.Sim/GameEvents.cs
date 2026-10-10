namespace HeightFall.Sim;

/// <summary>逻辑层发生的事件，供渲染层表现特效，由 World.DrainEvents 取走。</summary>
public abstract record GameEvent;

/// <summary>Amount 为护甲和魔抗减免后的实际伤害。</summary>
public sealed record DamageEvent(Unit Source, Unit Target, float Amount, bool FromSkill) : GameEvent;

public sealed record DeathEvent(Unit Unit) : GameEvent;

/// <summary>技能的一个效果开始生效；CasterPos 是生效前施法者所在的格子（位移类效果的起点）。</summary>
public sealed record EffectEvent(SkillCast Cast, SkillEffect Effect, GridPos CasterPos) : GameEvent;

/// <summary>空中单位落地。</summary>
public sealed record LandEvent(Unit Unit) : GameEvent;

/// <summary>单位坠落着地（走下高崖，或脚下格子突然下沉），Drop 为落差级数。</summary>
public sealed record FallEvent(Unit Unit, int Drop) : GameEvent;

/// <summary>回城读条完成，英雄已回到泉水。</summary>
public sealed record RecallEvent(Unit Unit) : GameEvent;

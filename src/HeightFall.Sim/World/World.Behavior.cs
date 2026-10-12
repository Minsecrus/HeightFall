namespace HeightFall.Sim;

public sealed partial class World
{
    public void OrderMove(Unit u, GridPos tile)
    {
        if (!u.IsAlive)
            return;
        CancelCast(u);
        u.RecallTicks = 0;
        u.Order = new MoveOrder(tile);
        u.AttackTarget = null;
        ClearPath(u);
    }

    public bool OrderAttack(Unit u, Unit target)
    {
        if (!u.IsAlive || !CanAttack(u, target))
            return false;
        CancelCast(u);
        u.RecallTicks = 0;
        u.Order = new AttackOrder(target);
        u.AttackTarget = target;
        ClearPath(u);
        return true;
    }

    public void OrderStop(Unit u)
    {
        CancelCast(u);
        u.RecallTicks = 0;
        u.Order = null;
        u.AttackTarget = null;
        ClearPath(u);
    }

    /// <summary>开始回城读条：原地站定，读完回到己方泉水。受到伤害、被眩晕或自己行动都会打断。</summary>
    public bool OrderRecall(Unit hero)
    {
        if (!hero.IsHero || !hero.IsAlive || hero.IsStunned || hero.IsAirborne || hero.IsRecalling)
            return false;
        CancelCast(hero);
        hero.Order = null;
        hero.AttackTarget = null;
        ClearPath(hero);
        hero.RecallTicks = SimTime.Ticks(GameRules.RecallSeconds);
        return true;
    }

    /// <summary>恢复：一段时间内持续回复生命和法力，可以边走边回，受到伤害即中断。</summary>
    public bool UseRestore(Unit hero)
    {
        if (!hero.IsHero || !hero.IsAlive || hero.RestoreCooldown > 0)
            return false;
        hero.RestoreTicks = SimTime.Ticks(GameRules.RestoreSeconds);
        hero.RestoreCooldown = SimTime.Ticks(GameRules.RestoreCooldownSeconds);
        return true;
    }

    void UpdateUnit(Unit u)
    {
        if (u.IsRemoved)
            return;
        if (u.IsDead)
        {
            if (--u.RespawnTicks <= 0)
                Respawn(u);
            return;
        }

        Regenerate(u);
        if (u.AttackCooldown > 0)
            u.AttackCooldown--;
        for (var i = 0; i < u.CooldownValues.Length; i++)
        {
            if (u.CooldownValues[i] > 0)
                u.CooldownValues[i]--;
        }
        if (u.RestoreCooldown > 0)
            u.RestoreCooldown--;
        if (u.StunImmuneTicks > 0)
            u.StunImmuneTicks--;
        if (u.SpeedBonusTicks > 0)
            u.SpeedBonusTicks--;

        var stunned = u.StunTicks > 0;
        if (stunned)
            u.StunTicks--;
        if (u.StepTicksLeft > 0 && --u.StepTicksLeft > 0)
            return;
        if (u.PendingFall > 0)
        {
            // 走下高崖的这一步刚落地
            Fall(u, u.PendingFall);
            u.PendingFall = 0;
            return;
        }
        if (u.AirborneTicks > 0)
        {
            if (--u.AirborneTicks > 0)
                return;
            u.PrevPos = u.Pos;
            Emit(new LandEvent(u));
        }
        if (stunned)
            return;
        if (u.IsCasting)
        {
            UpdateCasting(u);
            return;
        }

        if (u.IsStructure)
        {
            if (u.Def.PhysicalAttack > 0)
                UpdateTower(u);
        }
        else if (u.IsCreep)
        {
            UpdateCreep(u);
        }
        else
        {
            UpdateHero(u);
        }
    }

    void Regenerate(Unit u)
    {
        if (u.IsStructure)
        {
            RegenerateStructure(u);
            return;
        }
        if (!u.IsHero)
            return;
        var hpRegen = u.Def.HpRegen;
        var manaRegen = u.Def.ManaRegen + u.Def.ManaRegenPerLevel * (u.Level - 1);
        if (InOwnFountain(u))
        {
            hpRegen += u.MaxHp * GameRules.FountainHealPerSecond;
            manaRegen += u.MaxMana * GameRules.FountainHealPerSecond;
        }
        if (u.RestoreTicks > 0)
        {
            u.RestoreTicks--;
            hpRegen += u.MaxHp * GameRules.RestorePerSecond;
            manaRegen += u.MaxMana * GameRules.RestorePerSecond;
        }
        u.Hp = Math.Min(u.MaxHp, u.Hp + hpRegen / SimTime.TicksPerSecond);
        u.Mana = Math.Min(u.MaxMana, u.Mana + manaRegen / SimTime.TicksPerSecond);
    }

    void Respawn(Unit hero)
    {
        var center = FountainCenter(hero.Team);
        hero.Pos = hero.PrevPos = FindStandableNear(center, 8) ?? center;
        hero.StepTicksLeft = 0;
        hero.IsDead = false;
        hero.Hp = hero.MaxHp;
        hero.Mana = hero.MaxMana;
        hero.LastEnemyHeroDamager = null;
        Occupy(hero);
    }

    /// <summary>
    /// 防御塔和泉水：优先打刚攻击过己方英雄的敌方英雄，其次打小兵，最后打英雄。不能移动，只选弹道打得到的目标。
    /// </summary>
    void UpdateTower(Unit tower)
    {
        var range = tower.Def.AttackRange;
        var target = tower.AttackTarget;
        if (AggroHero(tower, range, mustHit: true) is { } aggroHero)
            target = aggroHero;
        else if (target is null || !IsValidTarget(tower, target) || Distance(tower, target) > range || !CanHit(tower, target))
            target = NearestEnemy(tower, range, mustHit: true, u => !u.IsHero && !u.IsStructure)
                ?? NearestEnemy(tower, range, mustHit: true, u => u.IsHero);

        tower.AttackTarget = target;
        if (target is not null)
            UpdateAttack(tower, target, chase: false);
    }

    /// <summary>范围内刚攻击过 u 方英雄的敌方英雄。</summary>
    Unit? AggroHero(Unit u, float range, bool mustHit) => NearestEnemy(u, range, mustHit,
        e => e.IsHero && Tick - e.LastAttackedHeroTick < SimTime.Ticks(GameRules.HeroAggroSeconds));

    /// <summary>
    /// 小兵：优先打刚攻击过己方英雄的敌方英雄，其次打小兵和建筑，附近没有别的目标时才打英雄；
    /// 追出太远就放弃。没有目标时沿兵线前进。
    /// </summary>
    void UpdateCreep(Unit creep)
    {
        var range = GameRules.CreepAggroRange;
        var target = creep.AttackTarget;
        if (target is not null && (!IsValidTarget(creep, target) || Distance(creep, target) > GameRules.CreepLeashRange))
            target = null;
        if (AggroHero(creep, range, mustHit: false) is { } aggroHero)
            target = aggroHero;
        else if (target is null || target.IsHero)
            target = NearestEnemy(creep, range, mustHit: false, u => !u.IsHero) ?? target
                ?? NearestEnemy(creep, range, mustHit: false, u => u.IsHero);
        creep.AttackTarget = target;

        if (target is not null)
        {
            UpdateAttack(creep, target, chase: true);
            return;
        }

        var points = creep.Waypoints;
        if (points is null)
            return;
        while (creep.WaypointIndex < points.Count - 1 && creep.Pos.DistanceTo(points[creep.WaypointIndex]) <= 2f)
            creep.WaypointIndex++;
        MoveToward(creep, points[creep.WaypointIndex], 0f);
    }

    void UpdateHero(Unit hero)
    {
        if (hero.IsRecalling)
        {
            if (--hero.RecallTicks == 0)
                FinishRecall(hero);
            return;
        }

        switch (hero.Order)
        {
            case MoveOrder move:
                if (MoveToward(hero, move.Target, 0f) != MoveResult.Moving)
                    hero.Order = null;
                break;
            case AttackOrder attack:
                if (!CanAttack(hero, attack.Target))
                {
                    hero.Order = null;
                    break;
                }
                UpdateAttack(hero, attack.Target, chase: true);
                if (hero.AttackTarget is null)
                    hero.Order = null;
                break;
            case CastOrder cast:
                UpdateCastOrder(hero, cast);
                break;
            default:
                UpdateIdleHero(hero);
                break;
        }
    }

    void FinishRecall(Unit hero)
    {
        var center = FountainCenter(hero.Team);
        if (FindStandableNear(center, 8) is not { } tile)
            return;
        Vacate(hero);
        hero.Pos = hero.PrevPos = tile;
        hero.StepTicksLeft = 0;
        Occupy(hero);
        Emit(new RecallEvent(hero));
    }

    /// <summary>空闲的英雄自动攻击射程内打得到的敌人，但不追击；有驻守点时走回驻守点。</summary>
    void UpdateIdleHero(Unit hero)
    {
        var target = hero.AttackTarget;
        if (target is null || !IsValidTarget(hero, target) || Distance(hero, target) > hero.Def.AttackRange || !CanHit(hero, target))
            target = NearestEnemy(hero, hero.Def.AttackRange, mustHit: true);
        hero.AttackTarget = target;

        if (target is not null)
            UpdateAttack(hero, target, chase: false);
        else if (hero.Post is { } post && hero.Pos.DistanceTo(post) > 2f)
            MoveToward(hero, post, 1f);
    }
}

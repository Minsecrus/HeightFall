namespace HeightFall.Sim;

public sealed partial class World
{
    /// <summary>可以被 attacker 主动选为攻击目标的敌方单位；受攻击顺序保护的建筑不能被选中。</summary>
    bool IsValidTarget(Unit attacker, Unit target) =>
        target.IsAlive && target.Team != attacker.Team && !target.IsAirborne && !target.Def.Invulnerable && !target.IsShielded;

    /// <summary>玩家指令允许的攻击：敌方单位，或血量低于一半的己方小兵（反补）。</summary>
    public bool CanAttack(Unit attacker, Unit target) =>
        IsValidTarget(attacker, target)
        || (target.IsAlive && target.Team == attacker.Team && target.IsCreep && target != attacker
            && target.Hp < target.MaxHp * GameRules.DenyHpFraction);

    /// <summary>近战总能打到；远程要求按当前位置打出的弹道不会撞上地形。</summary>
    bool CanHit(Unit u, Unit target) => u.Def.ProjectileSpeed <= 0 || Ballistics.IsClear(Map, u, target);

    /// <summary>射程内最近的敌方单位；mustHit 时只考虑现在就能打到的（不会追击的单位用）。</summary>
    Unit? NearestEnemy(Unit u, float range, bool mustHit, Func<Unit, bool>? accept = null)
    {
        Unit? best = null;
        var bestDistance = float.MaxValue;
        foreach (var other in _units)
        {
            if (!IsValidTarget(u, other) || (accept is not null && !accept(other)))
                continue;
            var d = Distance(u, other);
            if (d <= range && d < bestDistance && (!mustHit || CanHit(u, other)))
            {
                best = other;
                bestDistance = d;
            }
        }
        return best;
    }

    /// <summary>
    /// 在射程内且能打到就攻击。不在射程内，或直线弹道会撞上地形时：chase 为 true 则走近目标，否则放弃目标。
    /// </summary>
    void UpdateAttack(Unit u, Unit target, bool chase)
    {
        var range = u.Def.AttackRange;
        var inRange = Distance(u, target) <= range;
        if (!inRange || !CanHit(u, target))
        {
            // 射程够但被地形挡住时继续靠近，直到弹道畅通
            if (!chase || MoveToward(u, target.NearestTileTo(u.Pos), inRange ? 0f : range) == MoveResult.Stuck)
                u.AttackTarget = null;
            return;
        }

        ClearPath(u);
        if (u.AttackCooldown > 0)
            return;
        u.AttackCooldown = SimTime.Ticks(u.Def.AttackInterval);
        if (u.Def.ProjectileSpeed > 0)
        {
            var arc = Ballistics.ArcHeight(u, System.Numerics.Vector2.Distance(u.Center, target.Center));
            _projectiles.Add(new Projectile(_nextId++, u, target, u.PhysicalAttack, u.Def.ProjectileSpeed,
                Ballistics.LaunchHeight(Map, u), arc));
        }
        else
        {
            DealDamage(u, target, u.PhysicalAttack, DamageType.Physical);
        }
    }

    /// <summary>投射物追踪目标飞行；途中撞上地形就消失，不造成伤害。</summary>
    void UpdateProjectiles()
    {
        foreach (var p in _projectiles)
        {
            p.PrevPosition = p.Position;
            p.PrevHeight = p.Height;
            if (!p.Target.IsAlive)
            {
                p.IsDone = true;
                continue;
            }
            var toTarget = p.Target.Center - p.Position;
            var remaining = toTarget.Length();
            var step = MathF.Min(remaining, p.SpeedPerTick);
            var total = p.Traveled + remaining;
            var aim = Ballistics.AimHeight(Map, p.Target);
            // 一帧最多飞半格多，分段检查，免得越过一格宽的墙
            var samples = Math.Max(1, (int)MathF.Ceiling(step / 0.25f));
            for (var i = 1; i <= samples; i++)
            {
                var traveled = p.Traveled + step * i / samples;
                var t = total > 0 ? traveled / total : 1f;
                p.Position = remaining > 0 ? p.PrevPosition + toTarget / remaining * (step * i / samples) : p.Target.Center;
                p.Height = Ballistics.HeightAt(p.LaunchHeight, aim, p.Arc, t);
                if (t < 1f && Ballistics.HitsTerrain(Map, p.Position, p.Height))
                {
                    p.IsDone = true;
                    Emit(new ProjectileBlockedEvent(p));
                    break;
                }
            }
            p.Traveled += step;
            if (!p.IsDone && step >= remaining)
            {
                p.IsDone = true;
                DealDamage(p.Source, p.Target, p.Damage, DamageType.Physical);
            }
        }
    }

    /// <summary>
    /// source 为空表示与单位无关的伤害（坠落），不算任何单位的最后一击。
    /// 建筑受后门保护时伤害降低；受攻击顺序保护的建筑不受伤害。
    /// </summary>
    internal void DealDamage(Unit? source, Unit target, float amount, DamageType type, bool fromSkill = false)
    {
        if (!target.IsAlive || target.Def.Invulnerable || target.IsShielded)
            return;
        if (source is { IsHero: true } && source.Team != target.Team)
        {
            target.LastEnemyHeroDamager = source;
            if (target.IsHero)
                source.LastAttackedHeroTick = Tick;
        }

        var dealt = amount * (1 - DefenseReduction(target.Defense(type)));
        if (target.IsStructure && IsBackdoorProtected(target))
            dealt *= 1 - GameRules.BackdoorDamageReduction;
        target.Hp -= dealt;
        // 受到伤害会打断回城和恢复
        target.RecallTicks = 0;
        target.RestoreTicks = 0;
        Emit(new DamageEvent(source, target, dealt, fromSkill));
        if (target.Hp <= 0)
            Kill(target, source);
    }

    internal void Stun(Unit u, float seconds)
    {
        if (!u.IsAlive || u.IsStructure || u.StunImmuneTicks > 0)
            return;
        u.StunTicks = Math.Max(u.StunTicks, SimTime.Ticks(seconds));
        u.RecallTicks = 0;
        if (u.IsCasting)
        {
            u.CastTicksLeft = -1;
            if (u.Order is CastOrder)
                u.Order = null;
        }
    }

    /// <summary>
    /// lastHit 是完成最后一击的单位，坠落致死时为空。己方最后一击算反补；否则死者这一生受过敌方英雄伤害时
    /// （中立单位受过任意英雄伤害时），击杀记给最后伤害过它的那个英雄，不是他完成最后一击则金币打折。
    /// 建筑不受影响，仍按最后一击结算。
    /// </summary>
    void Kill(Unit victim, Unit? lastHit)
    {
        victim.Hp = 0;
        Vacate(victim);
        ClearPath(victim);
        victim.Order = null;
        victim.AttackTarget = null;
        victim.CastTicksLeft = -1;
        victim.StunTicks = 0;
        victim.AirborneTicks = 0;
        victim.PendingFall = 0;
        victim.RecallTicks = 0;
        victim.RestoreTicks = 0;
        var denied = lastHit is not null && lastHit.Team == victim.Team;
        var killer = denied || victim.IsStructure ? lastHit : victim.LastEnemyHeroDamager ?? lastHit;
        var goldFactor = killer == lastHit ? 1f : GameRules.CreditedKillGoldFactor;
        RecordKill(victim, killer, denied);
        GrantRewards(victim, killer, denied, goldFactor);
        Emit(new DeathEvent(victim));

        if (victim.IsHero)
        {
            victim.IsDead = true;
            victim.RespawnTicks = SimTime.Ticks(GameRules.RespawnSeconds(victim.Level));
        }
        else
        {
            victim.IsRemoved = true;
        }
        if (victim.IsStructure)
            OnStructureDestroyed(victim);
    }

    void RecordKill(Unit victim, Unit? killer, bool denied)
    {
        if (victim.IsHero)
        {
            victim.Deaths++;
            if (killer is not null && !denied)
                _teamKills[killer.Team] = TeamKills(killer.Team) + 1;
            if (killer is { IsHero: true } && !denied)
                killer.Kills++;
        }
        else if (victim.IsCreep && killer is { IsHero: true })
        {
            if (denied)
                killer.Denies++;
            else
                killer.LastHits++;
        }
    }

    /// <summary>金币给击杀者，摧毁建筑则击杀方全队都有；反补和没有击杀者时没有奖励。</summary>
    void GrantRewards(Unit victim, Unit? killer, bool denied, float goldFactor)
    {
        if (denied || killer is null)
            return;
        if (victim.IsStructure)
        {
            foreach (var hero in _units.Where(h => h.IsHero && h.Team == killer.Team))
                Earn(hero, GameRules.StructureGold);
        }
        else if (killer.IsHero)
        {
            var gold = victim.IsHero ? GameRules.HeroKillGold(victim.Level) : victim.Def.Gold;
            Earn(killer, (int)(gold * goldFactor));
        }
    }

    /// <summary>英雄获得金币，同时获得等量经验。开局金币和出售装备的退款不经过这里，不加经验。</summary>
    void Earn(Unit hero, int gold)
    {
        hero.Gold += gold;
        AddXp(hero, gold);
    }

    void AddXp(Unit hero, int amount)
    {
        hero.Xp += amount;
        while (hero.Level < GameRules.MaxHeroLevel && hero.Xp >= GameRules.XpToNext(hero.Level))
        {
            hero.Xp -= GameRules.XpToNext(hero.Level);
            var hpRatio = hero.Hp / hero.MaxHp;
            var manaRatio = hero.MaxMana > 0 ? hero.Mana / hero.MaxMana : 0;
            hero.Level++;
            hero.SkillPoints++;
            hero.Hp = hpRatio * hero.MaxHp;
            hero.Mana = manaRatio * hero.MaxMana;
        }
        if (hero.Level >= GameRules.MaxHeroLevel)
            hero.Xp = 0;
    }

    /// <summary>物防和法防共用的减伤比例。</summary>
    static float DefenseReduction(float defense) => 0.06f * defense / (1 + 0.06f * MathF.Abs(defense));
}

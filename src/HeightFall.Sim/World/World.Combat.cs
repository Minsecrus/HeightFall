namespace HeightFall.Sim;

public sealed partial class World
{
    /// <summary>可以被 attacker 主动选为攻击目标的敌方单位。</summary>
    bool IsValidTarget(Unit attacker, Unit target) =>
        target.IsAlive && target.Team != attacker.Team && !target.IsAirborne && !target.Def.Invulnerable;

    /// <summary>玩家指令允许的攻击：敌方单位，或血量低于一半的己方小兵（反补）。</summary>
    public bool CanAttack(Unit attacker, Unit target) =>
        IsValidTarget(attacker, target)
        || (target.IsAlive && target.Team == attacker.Team && target.IsCreep && target != attacker
            && target.Hp < target.MaxHp * GameRules.DenyHpFraction);

    Unit? NearestEnemy(Unit u, float range)
    {
        Unit? best = null;
        var bestDistance = float.MaxValue;
        foreach (var other in _units)
        {
            if (!IsValidTarget(u, other))
                continue;
            var d = Distance(u, other);
            if (d <= range && d < bestDistance)
            {
                best = other;
                bestDistance = d;
            }
        }
        return best;
    }

    /// <summary>在射程内就攻击；不在射程内时，chase 为 true 则追上去，否则放弃目标。</summary>
    void UpdateAttack(Unit u, Unit target, bool chase)
    {
        var range = u.Def.AttackRange;
        if (Distance(u, target) > range)
        {
            if (!chase || MoveToward(u, target.NearestTileTo(u.Pos), range) == MoveResult.Stuck)
                u.AttackTarget = null;
            return;
        }

        ClearPath(u);
        if (u.AttackCooldown > 0)
            return;
        u.AttackCooldown = SimTime.Ticks(u.Def.AttackInterval);
        if (u.Def.ProjectileSpeed > 0)
            _projectiles.Add(new Projectile(_nextId++, u, target, u.Damage, u.Def.ProjectileSpeed));
        else
            DealDamage(u, target, u.Damage, DamageType.Physical);
    }

    void UpdateProjectiles()
    {
        foreach (var p in _projectiles)
        {
            p.PrevPosition = p.Position;
            if (!p.Target.IsAlive)
            {
                p.IsDone = true;
                continue;
            }
            var toTarget = p.Target.Center - p.Position;
            var distance = toTarget.Length();
            if (distance <= p.SpeedPerTick)
            {
                p.Position = p.Target.Center;
                p.IsDone = true;
                DealDamage(p.Source, p.Target, p.Damage, DamageType.Physical);
            }
            else
            {
                p.Position += toTarget / distance * p.SpeedPerTick;
            }
        }
    }

    internal void DealDamage(Unit source, Unit target, float amount, DamageType type, bool fromSkill = false)
    {
        if (!target.IsAlive || target.Def.Invulnerable)
            return;
        if (source.IsHero && target.IsHero && source.Team != target.Team)
            source.LastAttackedHeroTick = Tick;

        var dealt = type switch
        {
            DamageType.Physical => amount * (1 - ArmorReduction(target.Armor)),
            DamageType.Magic => amount * (1 - target.Def.MagicResist),
            _ => amount,
        };
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

    void Kill(Unit victim, Unit killer)
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
        RecordKill(victim, killer);
        GrantRewards(victim, killer);
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
    }

    void RecordKill(Unit victim, Unit killer)
    {
        var denied = killer.Team == victim.Team;
        if (victim.IsHero)
        {
            victim.Deaths++;
            if (!denied)
                _teamKills[killer.Team] = TeamKills(killer.Team) + 1;
            if (killer.IsHero && !denied)
                killer.Kills++;
        }
        else if (victim.IsCreep && killer.IsHero)
        {
            if (denied)
                killer.Denies++;
            else
                killer.LastHits++;
        }
    }

    /// <summary>经验由附近的敌方英雄平分（反补只给一半）；金钱给完成最后一击的英雄，摧毁建筑则全队都有。</summary>
    void GrantRewards(Unit victim, Unit killer)
    {
        var denied = killer.Team == victim.Team;
        var xp = victim.IsHero ? GameRules.HeroKillXp(victim.Level) : victim.Def.Xp;
        if (denied)
            xp = (int)(xp * GameRules.DenyXpFactor);
        var nearby = _units
            .Where(h => h.IsHero && h.IsAlive && h.Team != victim.Team && Distance(h, victim) <= GameRules.XpShareRadius)
            .ToList();
        foreach (var hero in nearby)
            AddXp(hero, xp / nearby.Count);

        if (denied)
            return;
        if (victim.IsStructure)
        {
            foreach (var hero in _units.Where(h => h.IsHero && h.Team == killer.Team))
                hero.Gold += GameRules.StructureGold;
        }
        else if (killer.IsHero)
        {
            killer.Gold += victim.IsHero ? GameRules.HeroKillGold(victim.Level) : victim.Def.Gold;
        }
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

    static float ArmorReduction(float armor) => 0.06f * armor / (1 + 0.06f * MathF.Abs(armor));
}

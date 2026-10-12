namespace HeightFall.Sim;

public sealed partial class World
{
    /// <summary>摧毁对方基地核心的一方；决出胜负后对局不再推进。</summary>
    public Team? Winner { get; private set; }

    /// <summary>
    /// 按攻击顺序刷新建筑的保护：同一路外侧的塔倒下后内侧的塔才能受到伤害；该路 T3 倒下后兵营才能受到伤害；
    /// 任意一座兵营倒下后 T4 才能受到伤害；两座 T4 都倒下后基地核心才能受到伤害。
    /// </summary>
    void RefreshShields()
    {
        foreach (var u in _units)
        {
            if (u.Structure is not { } s)
                continue;
            u.IsShielded = s.Kind switch
            {
                StructureKind.Tower when s.Tier < 4 =>
                    AnyStanding(s.Team, x => x.Kind == StructureKind.Tower && x.Lane == s.Lane && x.Tier < s.Tier),
                StructureKind.Tower => !AnyFallen(s.Team, x => x.Kind == StructureKind.Barracks),
                StructureKind.Barracks =>
                    AnyStanding(s.Team, x => x.Kind == StructureKind.Tower && x.Lane == s.Lane && x.Tier == 3),
                StructureKind.Ancient => AnyStanding(s.Team, x => x.Kind == StructureKind.Tower && x.Tier == 4),
                _ => false,
            };
        }
    }

    bool AnyStanding(Team team, Func<Structure, bool> match) =>
        _units.Any(u => u.Structure is { } s && s.Team == team && match(s) && u.IsAlive);

    bool AnyFallen(Team team, Func<Structure, bool> match) =>
        Layout.Structures.Count(s => s.Team == team && match(s)) > _units.Count(u => u.Structure is { } s && s.Team == team && match(s) && u.IsAlive);

    void OnStructureDestroyed(Unit structure)
    {
        RefreshShields();
        if (structure.Structure is { Kind: StructureKind.Ancient } && Winner is null)
        {
            Winner = structure.Team.Opponent();
            Emit(new GameOverEvent(Winner.Value));
        }
    }

    /// <summary>后门保护：建筑周围一定范围内没有敌方小兵时，受到的伤害降低，并持续回复生命。</summary>
    bool IsBackdoorProtected(Unit structure) => !_units.Any(u =>
        u.IsCreep && u.IsAlive && u.Team != structure.Team && u.Team != Team.Neutral
        && Distance(u, structure) <= GameRules.BackdoorRadius);

    void RegenerateStructure(Unit structure)
    {
        if (structure.Hp >= structure.MaxHp || structure.Def.Invulnerable || !IsBackdoorProtected(structure))
            return;
        structure.Hp = Math.Min(structure.MaxHp,
            structure.Hp + structure.MaxHp * GameRules.BackdoorRegenPerSecond / SimTime.TicksPerSecond);
    }
}

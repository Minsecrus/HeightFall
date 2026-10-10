namespace HeightFall.Sim;

public sealed partial class World
{
    static readonly Lane[] AllLanes = [Lane.Top, Lane.Mid, Lane.Bot];

    void SpawnWavesIfDue()
    {
        if (Tick % SimTime.Ticks(GameRules.WaveIntervalSeconds) != 0)
            return;
        foreach (var team in new[] { Team.Heights, Team.Fall })
        foreach (var lane in AllLanes)
            SpawnWave(team, lane);
    }

    /// <summary>
    /// 每波 3 近战 + 1 远程 + 1 特殊兵：4:00 前是中程兵，之后是炮车兵；
    /// 敌方在这一路的兵营被摧毁后换成超级兵。
    /// </summary>
    void SpawnWave(Team team, Lane lane)
    {
        var enemy = team.Opponent();
        var special = !IsStructureAlive(enemy, StructureKind.Barracks, lane) ? UnitDefs.SuperCreep
            : Tick < SimTime.Ticks(GameRules.SiegeFromSeconds) ? UnitDefs.MidCreep
            : UnitDefs.SiegeCreep;
        UnitDef[] wave = [UnitDefs.MeleeCreep, UnitDefs.MeleeCreep, UnitDefs.MeleeCreep, UnitDefs.RangedCreep, special];

        var enemyAncient = Layout.Structures.First(s => s.Team == enemy && s.Kind == StructureKind.Ancient);
        var waypoints = LanePaths.For(team, lane, enemyAncient.Origin);
        var scale = 1f + GameRules.CreepUpgradeStep * (Tick / SimTime.Ticks(GameRules.CreepUpgradeSeconds));

        foreach (var def in wave)
        {
            if (FindStandableNear(waypoints[0], 4) is not { } tile)
                continue;
            var creep = AddUnit(def, team, tile);
            creep.Lane = lane;
            creep.Waypoints = waypoints;
            creep.StatScale = scale;
            creep.Hp = creep.MaxHp;
        }
    }

    bool IsStructureAlive(Team team, StructureKind kind, Lane lane) =>
        _units.Any(u => u.Structure is { } s && s.Team == team && s.Kind == kind && s.Lane == lane && u.IsAlive);

    void GrantPassiveGold()
    {
        if (Tick == 0 || Tick % SimTime.Ticks(GameRules.PassiveGoldIntervalSeconds) != 0)
            return;
        foreach (var hero in _units.Where(u => u.IsHero))
            hero.Gold += GameRules.PassiveGold;
    }
}

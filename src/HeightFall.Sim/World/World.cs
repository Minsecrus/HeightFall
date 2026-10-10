namespace HeightFall.Sim;

/// <summary>
/// 一局游戏的全部逻辑状态，以 30 Hz 固定帧推进。渲染层只读取状态、下达指令。
/// </summary>
public sealed partial class World
{
    readonly List<Unit> _units = [];
    readonly List<Projectile> _projectiles = [];
    readonly List<Telegraph> _telegraphs = [];
    readonly List<(int Tick, Action Action)> _scheduled = [];
    readonly List<GameEvent> _events = [];
    readonly Dictionary<Team, Unit> _fountains = [];
    readonly Dictionary<Team, int> _teamKills = [];
    readonly Unit?[] _occupant;
    readonly Pathfinder _pathfinder;
    int _nextId = 1;

    public World(MapLayout layout)
    {
        Layout = layout;
        Map = layout.Map;
        _occupant = new Unit?[Map.SizeX * Map.SizeY];
        _pathfinder = new Pathfinder(Map);
        Map.HeightChanged += OnHeightChanged;

        foreach (var s in layout.Structures)
        {
            var unit = AddUnit(UnitDefs.ForStructure(s), s.Team, s.Origin);
            unit.Lane = s.Lane;
            unit.Structure = s;
            if (s.Kind == StructureKind.Fountain)
                _fountains[s.Team] = unit;
        }
    }

    public MapLayout Layout { get; }
    public GameMap Map { get; }
    public int Tick { get; private set; }
    public float Seconds => SimTime.Seconds(Tick);
    public IReadOnlyList<Unit> Units => _units;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public IReadOnlyList<Telegraph> Telegraphs => _telegraphs;

    /// <summary>该队击杀敌方英雄的总数。</summary>
    public int TeamKills(Team team) => _teamKills.GetValueOrDefault(team);

    /// <summary>取走上次调用以来发生的全部事件。</summary>
    public List<GameEvent> DrainEvents()
    {
        var events = new List<GameEvent>(_events);
        _events.Clear();
        return events;
    }

    internal void Emit(GameEvent e) => _events.Add(e);

    public void Step()
    {
        Map.Tick();
        RunScheduled();
        SpawnWavesIfDue();
        GrantPassiveGold();
        for (var i = 0; i < _units.Count; i++)
            UpdateUnit(_units[i]);
        UpdateProjectiles();
        _units.RemoveAll(u => u.IsRemoved);
        _projectiles.RemoveAll(p => p.IsDone);
        Tick++;
        _telegraphs.RemoveAll(t => t.EndTick < Tick);
    }

    /// <summary>在己方泉水生成英雄，默认召唤师技能为闪现；post 为空闲时返回的驻守点。</summary>
    public Unit SpawnHero(UnitDef def, Team team, GridPos? post = null)
    {
        var center = FountainCenter(team);
        var hero = AddUnit(def, team, FindStandableNear(center, 8) ?? center);
        hero.Post = post;
        hero.Summoner = Summoners.Flash;
        hero.SkillLevelValues[Unit.SummonerSlot] = 1;
        return hero;
    }

    /// <summary>召唤师技能只能在己方泉水里更换。</summary>
    public bool CanChangeSummoner(Unit hero) => hero.IsAlive && InOwnFountain(hero);

    public bool SetSummoner(Unit hero, SkillDef skill)
    {
        if (!CanChangeSummoner(hero))
            return false;
        hero.Summoner = skill;
        hero.SkillLevelValues[Unit.SummonerSlot] = 1;
        return true;
    }

    /// <summary>占据该格的单位或建筑。</summary>
    public Unit? UnitAt(GridPos p) => Map.InBounds(p) ? _occupant[Index(p)] : null;

    /// <summary>两个单位占地之间的最近距离（格）。</summary>
    public static float Distance(Unit a, Unit b) => FootprintDistance(a.Pos, a.Size, b.Pos, b.Size);

    public static float DistanceToTile(Unit u, GridPos p) => FootprintDistance(u.Pos, u.Size, p, 1);

    /// <summary>位移落点、出生点：只要求格子在地图内且没被占用，高度不限。</summary>
    internal bool IsStandable(GridPos p) => Map.InBounds(p) && _occupant[Index(p)] is null;

    /// <summary>离 center 最近的可站立格；accept 可以再加条件（例如落点高度）。</summary>
    internal GridPos? FindStandableNear(GridPos center, int maxRadius, Func<GridPos, bool>? accept = null)
    {
        for (var r = 0; r <= maxRadius; r++)
        {
            GridPos? best = null;
            var bestDistance = float.MaxValue;
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    continue;
                var p = new GridPos(center.X + dx, center.Y + dy);
                var d = center.DistanceTo(p);
                if (d < bestDistance && IsStandable(p) && (accept is null || accept(p)))
                {
                    best = p;
                    bestDistance = d;
                }
            }
            if (best is not null)
                return best;
        }
        return null;
    }

    internal IReadOnlyList<Unit> UnitsAffected(SkillCast cast, EffectTargets targets)
    {
        var caster = cast.Caster;
        if (targets == EffectTargets.Caster)
            return caster.IsAlive ? [caster] : [];
        var tiles = cast.Tiles.ToHashSet();
        return _units.Where(u => u.IsAlive && !u.IsStructure && !u.IsAirborne && tiles.Contains(u.Pos) && targets switch
            {
                EffectTargets.Enemies => u.Team != caster.Team,
                EffectTargets.EnemyCreeps => u.Team != caster.Team && u.IsCreep,
                _ => u.Team == caster.Team && u.IsHero,
            })
            .ToList();
    }

    internal void Schedule(int delayTicks, Action action) => _scheduled.Add((Tick + delayTicks, action));

    void RunScheduled()
    {
        var due = _scheduled.Where(s => s.Tick <= Tick).ToList();
        if (due.Count == 0)
            return;
        _scheduled.RemoveAll(s => s.Tick <= Tick);
        foreach (var (_, action) in due)
            action();
    }

    /// <summary>脚下格子突然下沉 ≥ 2 级的单位坠落；地形效果到期的缓慢回落不算。</summary>
    void OnHeightChanged(GridPos p, int oldHeight, int newHeight, bool restoring)
    {
        if (restoring || oldHeight - newHeight < HeightRules.FallThreshold)
            return;
        if (UnitAt(p) is { IsStructure: false, IsAirborne: false, IsAlive: true } unit && unit.Pos == p)
            Fall(unit, oldHeight - newHeight);
    }

    internal void Fall(Unit u, int drop)
    {
        Stun(u, HeightRules.FallStunSeconds(drop));
        Emit(new FallEvent(u, drop));
    }

    Unit AddUnit(UnitDef def, Team team, GridPos pos)
    {
        var unit = new Unit(_nextId++, def, team, pos);
        _units.Add(unit);
        Occupy(unit);
        return unit;
    }

    void Occupy(Unit u)
    {
        if (!u.Def.BlocksTiles)
            return;
        foreach (var t in u.Footprint())
            _occupant[Index(t)] = u;
    }

    void Vacate(Unit u)
    {
        foreach (var t in u.Footprint())
        {
            if (Map.InBounds(t) && _occupant[Index(t)] == u)
                _occupant[Index(t)] = null;
        }
    }

    GridPos FountainCenter(Team team)
    {
        var f = _fountains[team];
        return new GridPos(f.Pos.X + f.Size / 2, f.Pos.Y + f.Size / 2);
    }

    bool InOwnFountain(Unit u) => _fountains.TryGetValue(u.Team, out var f) && f.Covers(u.Pos);

    int Index(GridPos p) => p.Y * Map.SizeX + p.X;

    static float FootprintDistance(GridPos a, int sizeA, GridPos b, int sizeB)
    {
        var dx = Math.Max(0, Math.Max(a.X - (b.X + sizeB - 1), b.X - (a.X + sizeA - 1)));
        var dy = Math.Max(0, Math.Max(a.Y - (b.Y + sizeB - 1), b.Y - (a.Y + sizeA - 1)));
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

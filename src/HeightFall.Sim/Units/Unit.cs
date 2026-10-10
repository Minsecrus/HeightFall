using System.Numerics;

namespace HeightFall.Sim;

public sealed class Unit
{
    /// <summary>召唤师技能所在的技能栏位，排在英雄自身的 4 个技能之后。</summary>
    public const int SummonerSlot = 4;

    internal readonly int[] SkillLevelValues;
    internal readonly int[] CooldownValues;

    internal Unit(int id, UnitDef def, Team team, GridPos pos)
    {
        Id = id;
        Def = def;
        Team = team;
        Pos = PrevPos = AirborneFrom = pos;
        SkillLevelValues = new int[def.Skills.Count + 1];
        CooldownValues = new int[def.Skills.Count + 1];
        if (def.Kind == UnitKind.Hero)
            SkillPoints = 1;
        Hp = MaxHp;
        Mana = MaxMana;
    }

    public int Id { get; }
    public UnitDef Def { get; }
    public Team Team { get; }
    public Lane Lane { get; internal set; }
    public Structure? Structure { get; internal set; }

    /// <summary>英雄开局前自选的召唤师技能，占用 SummonerSlot 栏位。</summary>
    public SkillDef? Summoner { get; internal set; }

    /// <summary>可用技能栏位数：英雄自身技能，有召唤师技能时再加一个。</summary>
    public int SkillCount => Def.Skills.Count + (Summoner is null ? 0 : 1);

    public SkillDef SkillAt(int slot) => slot < Def.Skills.Count ? Def.Skills[slot] : Summoner!;

    /// <summary>占据的格子（建筑为占地西南角）。迈步时立即占据下一格，PrevPos 是迈步前的格子。</summary>
    public GridPos Pos { get; internal set; }

    public GridPos PrevPos { get; internal set; }
    public int StepTicks { get; internal set; }
    public int StepTicksLeft { get; internal set; }

    /// <summary>这一步走下高崖时的落差级数，走完这一步时坠落。</summary>
    internal int PendingFall { get; set; }

    public float Hp { get; internal set; }
    public float Mana { get; internal set; }
    public int Level { get; internal set; } = 1;

    /// <summary>当前等级内已获得的经验。</summary>
    public int Xp { get; internal set; }

    public int Gold { get; internal set; }
    public int Kills { get; internal set; }
    public int Deaths { get; internal set; }
    public int LastHits { get; internal set; }
    public int Denies { get; internal set; }
    public int SkillPoints { get; internal set; }
    public IReadOnlyList<int> SkillLevels => SkillLevelValues;

    /// <summary>每个技能剩余的冷却帧数。</summary>
    public IReadOnlyList<int> SkillCooldowns => CooldownValues;

    public Order? Order { get; internal set; }
    public Unit? AttackTarget { get; internal set; }
    public int AttackCooldown { get; internal set; }
    public int StunTicks { get; internal set; }

    /// <summary>免疫眩晕（含坠落眩晕）的剩余帧数。</summary>
    public int StunImmuneTicks { get; internal set; }

    /// <summary>回城读条剩余帧数；读完回到己方泉水，0 表示没有在回城。</summary>
    public int RecallTicks { get; internal set; }

    /// <summary>恢复剩余帧数；期间持续回复生命和法力，受到伤害即中断。</summary>
    public int RestoreTicks { get; internal set; }

    public int RestoreCooldown { get; internal set; }

    /// <summary>移速加成（比例）及剩余帧数。</summary>
    public float SpeedBonus { get; internal set; }

    public int SpeedBonusTicks { get; internal set; }
    public int AirborneTicks { get; internal set; }
    public int AirborneTotal { get; internal set; }
    public GridPos AirborneFrom { get; internal set; }

    /// <summary>施法前摇剩余帧数，-1 表示没有在施法。</summary>
    public int CastTicksLeft { get; internal set; } = -1;

    public bool IsDead { get; internal set; }
    public int RespawnTicks { get; internal set; }
    public bool IsRemoved { get; internal set; }

    /// <summary>空闲时返回的驻守点。</summary>
    public GridPos? Post { get; internal set; }

    internal float StatScale { get; set; } = 1f;
    internal int LastAttackedHeroTick { get; set; } = int.MinValue / 2;

    internal List<GridPos>? Path { get; set; }
    internal int PathIndex { get; set; }
    internal GridPos PathGoal { get; set; }
    internal int WaitTicks { get; set; }
    internal int NextRepathTick { get; set; }

    internal IReadOnlyList<GridPos>? Waypoints { get; set; }
    internal int WaypointIndex { get; set; }

    public int Size => Def.Size;
    public bool IsAlive => !IsDead && !IsRemoved;
    public bool IsHero => Def.Kind == UnitKind.Hero;
    public bool IsCreep => Def.Kind.IsCreep();
    public bool IsStructure => Def.Kind.IsStructure();
    public bool IsStepping => StepTicksLeft > 0;
    public bool IsAirborne => AirborneTicks > 0;
    public bool IsStunned => StunTicks > 0;
    public bool IsRecalling => RecallTicks > 0;
    public bool IsRestoring => RestoreTicks > 0;
    public float MoveSpeed => Def.MoveSpeed * (1 + (SpeedBonusTicks > 0 ? SpeedBonus : 0));
    public bool IsCasting => CastTicksLeft >= 0;

    public float MaxHp => (Def.MaxHp + Def.HpPerLevel * (Level - 1)) * StatScale;
    public float MaxMana => Def.MaxMana + Def.ManaPerLevel * (Level - 1);
    public float Damage => (Def.Damage + Def.DamagePerLevel * (Level - 1)) * StatScale;
    public float Armor => Def.Armor + Def.ArmorPerLevel * (Level - 1);

    /// <summary>格子坐标系中的中心点，格子 (x, y) 的中心是 (x + 0.5, y + 0.5)。</summary>
    public Vector2 Center => new(Pos.X + Size / 2f, Pos.Y + Size / 2f);

    public IEnumerable<GridPos> Footprint()
    {
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
            yield return new GridPos(Pos.X + x, Pos.Y + y);
    }

    public bool Covers(GridPos p) =>
        p.X >= Pos.X && p.X < Pos.X + Size && p.Y >= Pos.Y && p.Y < Pos.Y + Size;

    /// <summary>占地中离 p 最近的格子。</summary>
    public GridPos NearestTileTo(GridPos p) =>
        new(Math.Clamp(p.X, Pos.X, Pos.X + Size - 1), Math.Clamp(p.Y, Pos.Y, Pos.Y + Size - 1));
}

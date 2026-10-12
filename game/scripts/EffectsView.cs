using System.Collections.Generic;
using System.Linq;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 技能和战斗特效：读取逻辑层事件，用纯色几何碎块、方形冲击波、格子闪光、伤害数字和震屏表现。
/// </summary>
public partial class EffectsView : Node3D
{
    const float Gravity = 18f;
    const int MaxParticles = 500;
    const float WaveStepSeconds = 0.035f;

    readonly List<Particle> _particles = [];
    readonly List<Ring> _rings = [];
    readonly List<TileFlash> _flashes = [];
    readonly List<FloatingNumber> _numbers = [];
    readonly Dictionary<Color, StandardMaterial3D> _solid = [];
    BoxMesh _cube = null!;
    ArrayMesh _shard = null!;
    BoxMesh _streak = null!;

    public World World { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;
    public UnitView Units { get; set; } = null!;
    public MapView Terrain { get; set; } = null!;
    public CameraRig CameraRig { get; set; } = null!;
    public TileOverlay Flashes { get; set; } = null!;

    sealed class Particle(MeshInstance3D node, Vector3 velocity, Vector3 spin, float life, float delay, bool gravity)
    {
        public MeshInstance3D Node { get; } = node;
        public Vector3 Velocity { get; set; } = velocity;
        public Vector3 Spin { get; } = spin;
        public float Life { get; set; } = life;
        public float MaxLife { get; } = life;
        public float Delay { get; set; } = delay;
        public bool Gravity { get; } = gravity;
        public Vector3 BaseScale { get; } = node.Scale;
    }

    sealed class Ring(Node3D node, StandardMaterial3D material, float from, float to, float life, float delay)
    {
        public Node3D Node { get; } = node;
        public StandardMaterial3D Material { get; } = material;
        public float From { get; } = from;
        public float To { get; } = to;
        public float Life { get; } = life;
        public float Age { get; set; } = -delay;
    }

    sealed record TileFlash(GridPos Tile, Color Color, float Life)
    {
        public float Age { get; set; }
    }

    sealed class FloatingNumber(Label3D label, float life)
    {
        public Label3D Label { get; } = label;
        public float Life { get; } = life;
        public float Age { get; set; }
    }

    public override void _Ready()
    {
        _cube = new BoxMesh { Size = Vector3.One };
        _shard = MeshUtil.Tetrahedron(0.6f);
        _streak = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 1.4f) };
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        foreach (var e in World.DrainEvents())
            Handle(e);
        UpdateParticles(dt);
        UpdateRings(dt);
        UpdateFlashes(dt);
        UpdateNumbers(dt);
    }

    void Handle(GameEvent e)
    {
        switch (e)
        {
            case DamageEvent d:
                OnDamage(d);
                break;
            case DeathEvent d:
                OnDeath(d.Unit);
                break;
            case EffectEvent fx:
                OnEffect(fx);
                break;
            case LandEvent land:
                var at = WorldSpace.TileCenter(World.Map, land.Unit.Pos);
                SpawnRing(at, 0.3f, 1.8f, Palette.Unit, 0.35f);
                Burst(at, 10, Palette.Unit, 4f, 0.12f);
                CameraRig.Shake(0.12f);
                break;
            case RecallEvent recall:
                var home = WorldSpace.TileCenter(World.Map, recall.Unit.Pos);
                SpawnRing(home, 0.2f, 1.6f, TeamColor(recall.Unit.Team), 0.45f);
                Burst(home + Vector3.Up * 0.6f, 12, TeamColor(recall.Unit.Team), 3.5f, 0.09f, upward: true);
                break;
            case ProjectileBlockedEvent blocked:
                // 投射物撞上地形，溅起几块碎屑
                var wall = new Vector3(blocked.Projectile.Position.X, blocked.Projectile.Height * WorldSpace.LevelHeight, -blocked.Projectile.Position.Y);
                Burst(wall, 4, Palette.Rock, 1.8f, 0.06f);
                break;
            case GameOverEvent:
                CameraRig.Shake(0.3f);
                break;
            case FallEvent fall:
                // 落差越大，尘土越多
                var ground = WorldSpace.TileCenter(World.Map, fall.Unit.Pos);
                SpawnRing(ground, 0.2f, 0.9f + 0.3f * fall.Drop, Palette.Rock, 0.3f);
                Burst(ground, 4 + 3 * fall.Drop, Palette.Rock, 2.5f + fall.Drop, 0.1f, upward: true);
                if (fall.Unit.IsHero)
                    CameraRig.Shake(0.05f * fall.Drop);
                break;
        }
    }

    void OnDamage(DamageEvent d)
    {
        Units.Flash(d.Target.Id);
        var target = UnitCenter(d.Target);
        if (!d.FromSkill && d.Source is { Def.ProjectileSpeed: 0f, IsStructure: false } melee)
            Units.Lunge(melee.Id, target - UnitCenter(melee));
        if (!d.Target.IsStructure)
            Burst(target, d.FromSkill ? 6 : 3, d.FromSkill && d.Source is not null ? TeamColor(d.Source.Team) : Palette.Unit, 2.5f, 0.08f);

        // 只显示与英雄有关的伤害，避免小兵互殴刷屏
        if (d.Source is { IsHero: true } || d.Target.IsHero)
        {
            var color = d.Target.Team == Viewer ? Palette.Enemy : Palette.Neutral;
            SpawnNumber(target + Vector3.Up * 1.4f, Mathf.RoundToInt(d.Amount), color, d.FromSkill ? 64 : 44);
        }
    }

    void OnDeath(Unit unit)
    {
        var center = UnitCenter(unit);
        if (unit.Structure is { } s)
        {
            var color = Palette.ForStructure(s);
            Burst(center + Vector3.Up, 26, color, 6f, 0.25f);
            Burst(center + Vector3.Up, 14, Palette.Unit, 5f, 0.2f);
            SpawnRing(center, 1f, 4f, color, 0.5f);
            CameraRig.Shake(0.35f);
        }
        else
        {
            Burst(center + Vector3.Up * 0.4f, unit.IsHero ? 16 : 9, Palette.Unit, 4f, unit.IsHero ? 0.16f : 0.12f);
        }
    }

    void OnEffect(EffectEvent fx)
    {
        var cast = fx.Cast;
        var team = TeamColor(cast.Caster.Team);
        var isLine = cast.Skill.Area is LineSelector;
        var map = World.Map;
        switch (fx.Effect)
        {
            case ShiftHeightEffect shift:
                for (var i = 0; i < cast.Tiles.Count; i++)
                {
                    var tile = cast.Tiles[i];
                    var delay = isLine ? fx.CasterPos.DistanceTo(tile) * WaveStepSeconds * 1.5f : 0f;
                    if (isLine)
                        Terrain.DelayAnimation(tile, delay);
                    var at = WorldSpace.TileCenter(map, tile);
                    var up = shift.Delta > 0;
                    Burst(at, up ? 3 : 2, Palette.Rock, up ? 5f : 2.5f, 0.12f, delay, upward: up);
                    AddFlash(tile, team with { A = 0.5f }, 0.4f, delay);
                }
                CameraRig.Shake(Mathf.Min(0.45f, 0.04f * cast.Tiles.Count * Mathf.Abs(shift.Delta) / 2f));
                break;

            case DamageEffect:
                if (isLine)
                {
                    for (var i = 0; i < cast.Tiles.Count; i++)
                    {
                        var delay = fx.CasterPos.DistanceTo(cast.Tiles[i]) * WaveStepSeconds;
                        AddFlash(cast.Tiles[i], team with { A = 0.7f }, 0.35f, delay);
                        Burst(WorldSpace.TileCenter(map, cast.Tiles[i]), 2, team, 3f, 0.1f, delay, upward: true);
                    }
                    if (cast.Skill.Icon == "arrow" && cast.Tiles.Count > 0)
                        SpawnStreak(fx.CasterPos, cast.Tiles.MaxBy(t => fx.CasterPos.DistanceTo(t)), team);
                }
                else
                {
                    var center = WorldSpace.TileCenter(map, cast.Target);
                    var radius = cast.Skill.Area is CircleSelector circle ? circle.Radius : 1f;
                    SpawnRing(center, 0.3f, radius + 0.6f, team, 0.4f);
                    foreach (var tile in cast.Tiles)
                        AddFlash(tile, team with { A = 0.6f }, 0.35f, 0f);
                    Burst(center, 8, team, 4f, 0.12f);
                }
                if (cast.Skill.IsUltimate)
                    CameraRig.Shake(0.3f);
                break;

            case BlinkEffect:
                var from = WorldSpace.TileCenter(map, fx.CasterPos) + Vector3.Up * 0.6f;
                var to = WorldSpace.TileCenter(map, cast.Caster.Pos) + Vector3.Up * 0.6f;
                Burst(from, 12, team, 4f, 0.1f);
                Burst(to, 12, team, 4f, 0.1f);
                SpawnRing(WorldSpace.TileCenter(map, cast.Caster.Pos), 0.2f, 1.3f, team, 0.3f);
                break;

            case LeapEffect:
                var takeoff = WorldSpace.TileCenter(map, fx.CasterPos);
                Burst(takeoff, 8, Palette.Unit, 3.5f, 0.1f);
                SpawnRing(takeoff, 0.2f, 1.2f, Palette.Unit, 0.3f);
                break;

            case HealEffect:
                var healAt = WorldSpace.TileCenter(map, cast.Caster.Pos);
                SpawnRing(healAt, 0.3f, (cast.Skill.Area as CircleSelector)?.Radius + 0.5f ?? 1f, team, 0.45f);
                foreach (var tile in cast.Tiles)
                    AddFlash(tile, team with { A = 0.3f }, 0.45f, 0f);
                Burst(healAt + Vector3.Up * 0.3f, 10, team, 3f, 0.09f, upward: true);
                break;

            case CleanseEffect:
                var cleanseAt = WorldSpace.TileCenter(map, cast.Caster.Pos);
                SpawnRing(cleanseAt, 0.2f, 1.4f, Palette.Unit, 0.35f);
                Burst(cleanseAt + Vector3.Up * 0.8f, 10, Palette.Unit, 3.5f, 0.09f);
                break;

            case SpeedBoostEffect:
                var sprintAt = WorldSpace.TileCenter(map, cast.Caster.Pos);
                SpawnRing(sprintAt, 0.2f, 1f, team, 0.3f);
                Burst(sprintAt, 6, team, 2.5f, 0.08f);
                break;

            // 只有眩晕、没有伤害的技能（晕眩）在这里画范围；带伤害的技能已由 DamageEffect 画过
            case StunEffect when !cast.Skill.Effects.OfType<DamageEffect>().Any():
                var stunAt = WorldSpace.TileCenter(map, cast.Target);
                SpawnRing(stunAt, 0.3f, (cast.Skill.Area as CircleSelector)?.Radius + 0.5f ?? 1f, team, 0.35f);
                foreach (var tile in cast.Tiles)
                    AddFlash(tile, team with { A = 0.5f }, 0.3f, 0f);
                CameraRig.Shake(0.1f);
                break;
        }
    }

    Color TeamColor(Team team) => Palette.ForTeam(team, Viewer);

    Vector3 UnitCenter(Unit u) =>
        WorldSpace.UnitPosition(World.Map, u, (float)Engine.GetPhysicsInterpolationFraction()) + Vector3.Up * 0.5f;

    /// <summary>向四周（upward 时主要向上）喷出纯色几何碎块。</summary>
    void Burst(Vector3 at, int count, Color color, float speed, float size, float delay = 0f, bool upward = false)
    {
        for (var i = 0; i < count && _particles.Count < MaxParticles; i++)
        {
            var dir = new Vector3(GD.Randf() * 2 - 1, upward ? 1.2f + GD.Randf() : GD.Randf() * 1.5f, GD.Randf() * 2 - 1)
                .Normalized();
            var node = new MeshInstance3D
            {
                Mesh = GD.Randf() < 0.5f ? _cube : _shard,
                MaterialOverride = Solid(color),
                Position = at,
                Scale = Vector3.One * size * (0.7f + GD.Randf() * 0.6f),
                Visible = delay <= 0,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(node);
            var spin = new Vector3(GD.Randf(), GD.Randf(), GD.Randf()) * 12f;
            _particles.Add(new Particle(node, dir * speed * (0.6f + GD.Randf() * 0.6f), spin, 0.6f + GD.Randf() * 0.4f,
                delay, gravity: true));
        }
    }

    /// <summary>穿云：一道光束从施法者沿直线飞到尽头。</summary>
    void SpawnStreak(GridPos from, GridPos to, Color color)
    {
        var start = WorldSpace.TileCenter(World.Map, from) + Vector3.Up * 0.7f;
        var end = WorldSpace.TileCenter(World.Map, to) + Vector3.Up * 0.7f;
        var node = new MeshInstance3D
        {
            Mesh = _streak,
            MaterialOverride = Solid(color, emission: 0.6f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(node);
        node.LookAtFromPosition(start, end, Vector3.Up);
        var travel = end - start;
        var life = travel.Length() / 40f;
        _particles.Add(new Particle(node, travel / life, Vector3.Zero, life, 0f, gravity: false));
    }

    /// <summary>贴地的方形冲击波，从 from 扩到 to（半边长，米）并淡出。</summary>
    void SpawnRing(Vector3 center, float from, float to, Color color, float life, float delay = 0f)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var node = new Node3D { Position = center + Vector3.Up * 0.06f, Visible = delay <= 0 };
        for (var i = 0; i < 4; i++)
        {
            node.AddChild(new MeshInstance3D
            {
                Mesh = _cube,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        AddChild(node);
        _rings.Add(new Ring(node, material, from, to, life, delay));
    }

    void AddFlash(GridPos tile, Color color, float life, float delay) =>
        _flashes.Add(new TileFlash(tile, color, life) { Age = -delay });

    void SpawnNumber(Vector3 at, int amount, Color color, int fontSize)
    {
        var label = new Label3D
        {
            Text = $"{amount}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = fontSize,
            PixelSize = 0.006f,
            Modulate = color,
            OutlineModulate = Colors.White,
            OutlineSize = 10,
            Position = at + new Vector3(GD.Randf() * 0.4f - 0.2f, 0, 0),
        };
        AddChild(label);
        _numbers.Add(new FloatingNumber(label, 0.8f));
    }

    void UpdateParticles(float dt)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            if (p.Delay > 0)
            {
                p.Delay -= dt;
                p.Node.Visible = p.Delay <= 0;
                continue;
            }
            p.Life -= dt;
            if (p.Life <= 0)
            {
                p.Node.QueueFree();
                _particles.RemoveAt(i);
                continue;
            }

            var v = p.Velocity;
            if (p.Gravity)
                v.Y -= Gravity * dt;
            var pos = p.Node.Position + v * dt;
            if (p.Gravity)
            {
                var ground = WorldSpace.GroundAt(World.Map, WorldSpace.ToGrid(pos));
                if (pos.Y < ground)
                {
                    pos.Y = ground;
                    v = new Vector3(v.X * 0.5f, -v.Y * 0.35f, v.Z * 0.5f);
                }
            }
            p.Velocity = v;
            p.Node.Position = pos;
            p.Node.Rotation += p.Spin * dt;
            // 最后 0.25 秒缩小消失
            p.Node.Scale = p.BaseScale * Mathf.Min(1f, p.Life / 0.25f);
        }
    }

    void UpdateRings(float dt)
    {
        for (var i = _rings.Count - 1; i >= 0; i--)
        {
            var r = _rings[i];
            r.Age += dt;
            if (r.Age < 0)
                continue;
            r.Node.Visible = true;
            var t = Mathf.Min(1f, r.Age / r.Life);
            if (t >= 1f)
            {
                r.Node.QueueFree();
                _rings.RemoveAt(i);
                continue;
            }
            var half = Mathf.Lerp(r.From, r.To, 1 - (1 - t) * (1 - t));
            const float thickness = 0.1f;
            var edges = r.Node.GetChildren().Cast<MeshInstance3D>().ToArray();
            edges[0].Position = new Vector3(0, 0, -half);
            edges[1].Position = new Vector3(0, 0, half);
            edges[2].Position = new Vector3(-half, 0, 0);
            edges[3].Position = new Vector3(half, 0, 0);
            edges[0].Scale = edges[1].Scale = new Vector3(2 * half + thickness, 0.04f, thickness);
            edges[2].Scale = edges[3].Scale = new Vector3(thickness, 0.04f, 2 * half + thickness);
            r.Material.AlbedoColor = r.Material.AlbedoColor with { A = 1 - t };
        }
    }

    void UpdateFlashes(float dt)
    {
        Flashes.Clear();
        for (var i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.Age += dt;
            if (f.Age < 0)
                continue;
            if (f.Age >= f.Life)
            {
                _flashes.RemoveAt(i);
                continue;
            }
            Flashes.Add(f.Tile, f.Color with { A = f.Color.A * (1 - f.Age / f.Life) });
        }
        Flashes.Commit();
    }

    void UpdateNumbers(float dt)
    {
        for (var i = _numbers.Count - 1; i >= 0; i--)
        {
            var n = _numbers[i];
            n.Age += dt;
            if (n.Age >= n.Life)
            {
                n.Label.QueueFree();
                _numbers.RemoveAt(i);
                continue;
            }
            var t = n.Age / n.Life;
            n.Label.Position += Vector3.Up * (1.6f * (1 - t) * dt);
            var alpha = t < 0.6f ? 1f : 1 - (t - 0.6f) / 0.4f;
            n.Label.Modulate = n.Label.Modulate with { A = alpha };
            n.Label.OutlineModulate = n.Label.OutlineModulate with { A = alpha };
        }
    }

    StandardMaterial3D Solid(Color color, float emission = 0f)
    {
        var key = emission > 0 ? color with { A = 0.999f } : color;
        if (_solid.TryGetValue(key, out var material))
            return material;
        material = new StandardMaterial3D { AlbedoColor = color with { A = 1f }, Roughness = 1f };
        if (emission > 0)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = emission;
        }
        return _solid[key] = material;
    }
}

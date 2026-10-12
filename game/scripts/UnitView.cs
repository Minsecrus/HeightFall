using System.Collections.Generic;
using System.Linq;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 单位、建筑和投射物的渲染：单位是白色几何体，建筑按阵营上色，英雄头顶显示等级、生命和能量。
/// 泉水没有模型，由 MapView 给格子染色。每帧按逻辑状态同步，并在两个逻辑帧之间插值。
/// 受击时闪一下并被压扁再弹回，近战攻击时向目标冲一下，眩晕时头顶有小四面体绕圈。
/// </summary>
public partial class UnitView : Node3D
{
    const float CreepHover = 0.15f;
    const float CreepSpin = 0.8f;
    const float FlashSeconds = 0.14f;
    const float LungeSeconds = 0.16f;
    const float LungeDistance = 0.3f;
    const float HealthBarOffset = 0.45f;
    const float HeroHealthBarOffset = 0.65f;
    // 相机俯角为 60°，世界竖距投到屏幕后约减半，需留出两条 billboard 的间隔。
    const float ManaBarSpacing = 0.36f;
    const float LevelBadgeDiameter = 0.36f;
    const float LevelBadgeGap = 0.04f;
    const int LevelBadgeTextureSize = 128;

    readonly Dictionary<int, Visual> _units = [];
    readonly Dictionary<int, MeshInstance3D> _projectiles = [];
    readonly Dictionary<UnitKind, Mesh> _creepMeshes = [];
    readonly Dictionary<(Color, float), StandardMaterial3D> _materials = [];
    StandardMaterial3D _white = null!;
    StandardMaterial3D _flash = null!;
    Mesh _projectileMesh = null!;
    Mesh _bigProjectileMesh = null!;
    Mesh _stunMesh = null!;
    Mesh _unitCube = null!;
    ImageTexture _levelBadgeTexture = null!;
    double _time;

    public World World { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;
    public bool TouchMode { get; set; }

    sealed class Visual(Node3D root, Node3D body, HealthBar? bar, Node3D? spinner, float height)
    {
        public Node3D Root { get; } = root;
        public Node3D Body { get; } = body;
        public HealthBar? Bar { get; } = bar;
        public HealthBar? ManaBar { get; set; }
        public Node3D? LevelBadge { get; set; }
        public Label3D? LevelLabel { get; set; }
        public int DisplayedLevel { get; set; } = -1;

        /// <summary>建筑上缓慢旋转、上下浮动的部件。</summary>
        public Node3D? Spinner { get; } = spinner;

        public float SpinnerY { get; } = spinner?.Position.Y ?? 0f;
        public float Height { get; } = height;
        public float Yaw { get; set; }
        public float FlashLeft { get; set; }
        public float LungeLeft { get; set; }
        public Vector3 LungeDir { get; set; }
        public Node3D? StunMarker { get; set; }
        public Node3D? RecallMarker { get; set; }
        public Node3D? RestoreMarker { get; set; }
    }

    public override void _Ready()
    {
        _white = new StandardMaterial3D { AlbedoColor = Palette.Unit, Roughness = 1f };
        _flash = new StandardMaterial3D
        {
            AlbedoColor = Palette.Hit with { A = 0.65f },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _creepMeshes[UnitKind.MeleeCreep] = new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.5f) };
        _creepMeshes[UnitKind.MidCreep] = MeshUtil.Tetrahedron(0.42f);
        _creepMeshes[UnitKind.RangedCreep] = MeshUtil.Octahedron(0.4f);
        _creepMeshes[UnitKind.SiegeCreep] = MeshUtil.Dodecahedron(0.45f);
        _creepMeshes[UnitKind.SuperCreep] = MeshUtil.Icosahedron(0.55f);
        _projectileMesh = new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 8, Rings = 4 };
        _bigProjectileMesh = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 8, Rings = 4 };
        _stunMesh = MeshUtil.Tetrahedron(0.1f);
        _unitCube = new BoxMesh { Size = Vector3.One };
        _levelBadgeTexture = CreateLevelBadgeTexture();
    }

    /// <summary>受击：闪一下，并被压扁再弹回。</summary>
    public void Flash(int unitId)
    {
        if (_units.TryGetValue(unitId, out var v))
            v.FlashLeft = FlashSeconds;
    }

    /// <summary>近战攻击：朝 direction 冲一下再退回。</summary>
    public void Lunge(int unitId, Vector3 direction)
    {
        if (!_units.TryGetValue(unitId, out var v))
            return;
        v.LungeLeft = LungeSeconds;
        v.LungeDir = new Vector3(direction.X, 0, direction.Z).Normalized();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        var frac = (float)Engine.GetPhysicsInterpolationFraction();
        var map = World.Map;

        var alive = new HashSet<int>();
        foreach (var u in World.Units)
        {
            alive.Add(u.Id);
            if (!_units.TryGetValue(u.Id, out var visual))
                _units[u.Id] = visual = Create(u);
            Sync(map, u, visual, frac, (float)delta);
        }
        foreach (var id in _units.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _units[id].Root.QueueFree();
            _units.Remove(id);
        }

        var flying = new HashSet<int>();
        foreach (var p in World.Projectiles)
        {
            flying.Add(p.Id);
            if (!_projectiles.TryGetValue(p.Id, out var mesh))
            {
                mesh = new MeshInstance3D
                {
                    Mesh = p.Source.IsStructure ? _bigProjectileMesh : _projectileMesh,
                    MaterialOverride = _white,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(mesh);
                _projectiles[p.Id] = mesh;
            }
            mesh.Position = WorldSpace.ProjectilePosition(p, frac);
        }
        foreach (var id in _projectiles.Keys.Where(id => !flying.Contains(id)).ToList())
        {
            _projectiles[id].QueueFree();
            _projectiles.Remove(id);
        }
    }

    void Sync(GameMap map, Unit u, Visual v, float frac, float delta)
    {
        v.Root.Visible = !u.IsDead;
        UpdateHeroStatus(u, v);
        if (u.IsDead)
            return;
        v.Root.Position = WorldSpace.UnitPosition(map, u, frac);
        v.Bar?.SetFill(u.Hp / u.MaxHp);

        if (v.LungeLeft > 0)
        {
            v.LungeLeft = Mathf.Max(0, v.LungeLeft - delta);
            v.Root.Position += v.LungeDir * (LungeDistance * Mathf.Sin(Mathf.Pi * (1 - v.LungeLeft / LungeSeconds)));
        }
        UpdateFlash(v, delta);
        UpdateStunMarker(u, v);
        if (u.IsHero)
            UpdateHeroMarkers(u, v);

        var t = (float)_time;
        if (v.Spinner is { } spinner)
        {
            spinner.Rotation = new Vector3(0, t * 0.4f, 0);
            spinner.Position = new Vector3(0, v.SpinnerY + 0.12f * Mathf.Sin(t * 1.2f + u.Id), 0);
        }
        if (u.IsCreep)
        {
            v.Body.Rotation = new Vector3(0, t * CreepSpin + u.Id, 0);
        }
        else if (u.IsHero)
        {
            var step = u.Pos - u.PrevPos;
            if (step != default)
                v.Yaw = Mathf.Atan2(-step.X, step.Y);
            // 被眩晕时左右摇晃
            var wobble = u.IsStunned ? Mathf.Sin(t * 25f) * 0.2f : 0f;
            v.Body.Rotation = new Vector3(0, v.Yaw, wobble);
        }
    }

    void UpdateHeroStatus(Unit u, Visual v)
    {
        if (!u.IsHero) return;
        var visible = u.MaxMana > 0f && u.IsAlive;
        if (visible && v.ManaBar is null)
        {
            v.ManaBar = new HealthBar
            {
                Width = v.Bar?.Width ?? 1f,
                BarColor = Palette.Mana,
                Position = new Vector3(0, v.Height + HeroHealthBarOffset - ManaBarSpacing, 0),
            };
            v.Root.AddChild(v.ManaBar);
        }
        if (v.Bar is { } health)
            health.Position = new Vector3(0, v.Height + HeroHealthBarOffset, 0);
        if (v.ManaBar is { } mana)
        {
            mana.Visible = visible;
            if (visible)
                mana.SetFill(u.Mana / u.MaxMana);
        }
        if (v.LevelBadge is null)
            CreateLevelBadge(v);
        var right = GetViewport().GetCamera3D()?.GlobalTransform.Basis.X ?? Vector3.Right;
        v.LevelBadge!.Position = new Vector3(0, v.Height + HeroHealthBarOffset, 0)
            - right * ((v.Bar?.Width ?? 1f) / 2f + LevelBadgeDiameter / 2f + LevelBadgeGap);
        v.LevelBadge.Visible = u.IsAlive;
        if (v.DisplayedLevel != u.Level)
        {
            v.DisplayedLevel = u.Level;
            v.LevelLabel!.Text = u.Level.ToString();
        }
    }

    void CreateLevelBadge(Visual v)
    {
        var badge = new Node3D();
        badge.AddChild(new Sprite3D
        {
            Texture = _levelBadgeTexture,
            PixelSize = LevelBadgeDiameter / LevelBadgeTextureSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = false,
            NoDepthTest = true,
            Shaded = false,
            RenderPriority = 1,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        var label = new Label3D
        {
            Font = ThemeDB.FallbackFont,
            FontSize = 48,
            PixelSize = 0.0038f,
            Modulate = Colors.White,
            OutlineSize = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = false,
            NoDepthTest = true,
            Shaded = false,
            RenderPriority = 2,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        badge.AddChild(label);
        v.Root.AddChild(badge);
        v.LevelBadge = badge;
        v.LevelLabel = label;
    }

    static ImageTexture CreateLevelBadgeTexture()
    {
        using var image = Image.CreateEmpty(LevelBadgeTextureSize, LevelBadgeTextureSize, false, Image.Format.Rgba8);
        var center = (LevelBadgeTextureSize - 1) / 2f;
        var radius = center - 1f;
        var black = new Color("#17191C");
        for (var y = 0; y < LevelBadgeTextureSize; y++)
        for (var x = 0; x < LevelBadgeTextureSize; x++)
        {
            var distance = new Vector2(x - center, y - center).Length();
            var alpha = Mathf.Clamp(radius + 0.5f - distance, 0f, 1f);
            var rim = Mathf.Clamp(distance - (radius - 6f), 0f, 1f);
            image.SetPixel(x, y, black.Lerp(Palette.Gold, rim) with { A = alpha });
        }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>闪光期间给模型叠一层受击色，并沿竖直方向压扁后弹回。</summary>
    void UpdateFlash(Visual v, float delta)
    {
        if (v.FlashLeft <= 0)
            return;
        v.FlashLeft = Mathf.Max(0, v.FlashLeft - delta);
        var k = v.FlashLeft / FlashSeconds;
        var squash = v.Spinner is null ? 0.18f * k : 0.06f * k;
        v.Body.Scale = new Vector3(1 + squash, 1 - squash, 1 + squash);
        var overlay = v.FlashLeft > 0 ? _flash : null;
        foreach (var mesh in v.Body.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false))
            ((MeshInstance3D)mesh).MaterialOverlay = overlay;
    }

    /// <summary>眩晕时头顶三个小四面体绕圈。</summary>
    void UpdateStunMarker(Unit u, Visual v)
    {
        if (u.IsStunned && v.StunMarker is null)
        {
            v.StunMarker = new Node3D { Position = new Vector3(0, v.Height + 0.2f, 0) };
            for (var i = 0; i < 3; i++)
            {
                var angle = Mathf.Tau * i / 3;
                v.StunMarker.AddChild(new MeshInstance3D
                {
                    Mesh = _stunMesh,
                    MaterialOverride = Material(Palette.Gold, emission: 0.4f),
                    Position = new Vector3(Mathf.Cos(angle) * 0.35f, 0, Mathf.Sin(angle) * 0.35f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
            v.Root.AddChild(v.StunMarker);
        }
        if (v.StunMarker is null)
            return;
        v.StunMarker.Visible = u.IsStunned;
        v.StunMarker.Rotation = new Vector3(0, (float)_time * 6f, 0);
    }

    /// <summary>
    /// 回城：脚下一圈队伍色方框随读条收拢；恢复：三个队伍色小方块绕身体缓缓上升。
    /// </summary>
    void UpdateHeroMarkers(Unit u, Visual v)
    {
        var color = Palette.ForTeam(u.Team, Viewer);
        if (u.IsRecalling && v.RecallMarker is null)
        {
            v.RecallMarker = new Node3D();
            for (var i = 0; i < 4; i++)
                v.RecallMarker.AddChild(new MeshInstance3D
                {
                    Mesh = _unitCube,
                    MaterialOverride = Material(color, emission: 0.4f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            v.Root.AddChild(v.RecallMarker);
        }
        if (v.RecallMarker is { } recall)
        {
            recall.Visible = u.IsRecalling;
            if (u.IsRecalling)
            {
                var progress = 1f - u.RecallTicks / (float)SimTime.Ticks(GameRules.RecallSeconds);
                var half = Mathf.Lerp(0.8f, 0.3f, progress);
                var edges = recall.GetChildren();
                for (var i = 0; i < 4; i++)
                {
                    var edge = (MeshInstance3D)edges[i];
                    var alongX = i < 2;
                    var sign = i % 2 == 0 ? 1 : -1;
                    edge.Position = alongX ? new Vector3(0, 0.05f, sign * half) : new Vector3(sign * half, 0.05f, 0);
                    edge.Scale = alongX ? new Vector3(2 * half, 0.05f, 0.06f) : new Vector3(0.06f, 0.05f, 2 * half);
                }
            }
        }

        if (u.IsRestoring && v.RestoreMarker is null)
        {
            v.RestoreMarker = new Node3D();
            for (var i = 0; i < 3; i++)
                v.RestoreMarker.AddChild(new MeshInstance3D
                {
                    Mesh = _unitCube,
                    MaterialOverride = Material(color, emission: 0.4f),
                    Scale = Vector3.One * 0.1f,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            v.Root.AddChild(v.RestoreMarker);
        }
        if (v.RestoreMarker is { } restore)
        {
            restore.Visible = u.IsRestoring;
            var t = (float)_time;
            var cubes = restore.GetChildren();
            for (var i = 0; i < 3; i++)
            {
                var angle = t * 2.5f + Mathf.Tau * i / 3;
                var rise = (t * 0.6f + i / 3f) % 1f;
                ((Node3D)cubes[i]).Position = new Vector3(Mathf.Cos(angle) * 0.55f, 0.2f + rise * 1.1f, Mathf.Sin(angle) * 0.55f);
            }
        }
    }

    Visual Create(Unit u)
    {
        var root = new Node3D();
        AddChild(root);

        Node3D? spinner = null;
        var (body, height, barWidth) = u.Def.Kind switch
        {
            UnitKind.Hero => (HeroModel(u.Def.Model), 1.3f, 1f),
            UnitKind.Fountain => (new Node3D(), 0f, 0f),
            _ when u.Structure is { } s => StructureModel(s, out spinner),
            _ => CreepModel(u.Def.Kind),
        };
        root.AddChild(body);

        HealthBar? bar = null;
        if (!u.Def.Invulnerable)
        {
            bar = new HealthBar
            {
                Width = barWidth,
                BarColor = Palette.ForTeam(u.Team, Viewer),
                Position = new Vector3(0, height + HealthBarOffset, 0),
            };
            root.AddChild(bar);
        }
        return new Visual(root, body, bar, spinner, height);
    }

    (Node3D Body, float Height, float BarWidth) CreepModel(UnitKind kind)
    {
        var mesh = _creepMeshes[kind];
        var radius = mesh.GetAabb().Size.Y / 2f;
        var body = new Node3D();
        var solid = Solid(mesh, radius + CreepHover);
        body.AddChild(solid);
        return (body, 2 * radius + CreepHover, 0.6f);
    }

    /// <summary>
    /// 建筑由多个几何体组合而成：白色底座和装饰，主体用阵营色，顶部有一个缓慢旋转的发光部件。
    /// 防御塔的层级越高，塔身上的白环越多。
    /// </summary>
    (Node3D Body, float Height, float BarWidth) StructureModel(Structure s, out Node3D? spinner)
    {
        var color = Palette.ForStructure(s);
        var main = Material(color);
        var shade = Material(color.Darkened(0.18f));
        var glow = Material(color, emission: 0.35f);
        var body = new Node3D();

        switch (s.Kind)
        {
            case StructureKind.Tower:
                body.AddChild(Box(1.7f, 0.3f, 1.7f, 0f, _white));
                body.AddChild(Box(0.9f, 1.8f, 0.9f, 0.3f, main));
                for (var i = 1; i < s.Tier; i++)
                    body.AddChild(Box(1.02f, 0.08f, 1.02f, 0.3f + 1.8f * i / s.Tier, _white));
                body.AddChild(Box(1.2f, 0.15f, 1.2f, 2.1f, _white));
                spinner = Part(MeshUtil.Octahedron(0.42f), glow, 2.75f);
                body.AddChild(spinner);
                return (body, 3.2f, 1.4f);

            case StructureKind.Barracks:
                body.AddChild(Box(2.8f, 0.25f, 2.8f, 0f, _white));
                body.AddChild(Box(2f, 0.8f, 2f, 0.25f, main));
                foreach (var (x, z) in Corners(1.15f))
                    body.AddChild(Box(0.32f, 1.2f, 0.32f, 0.25f, shade, x, z));
                body.AddChild(Part(new PrismMesh { Size = new Vector3(2.2f, 0.6f, 2.2f) }, _white, 1.35f));
                spinner = null;
                return (body, 1.65f, 2f);

            default:
                body.AddChild(Box(3.6f, 0.25f, 3.6f, 0f, _white));
                body.AddChild(Box(2.8f, 0.25f, 2.8f, 0.25f, _white));
                body.AddChild(Box(2f, 0.2f, 2f, 0.5f, shade));
                foreach (var (x, z) in Corners(1.55f))
                    body.AddChild(Box(0.35f, 1.5f, 0.35f, 0.25f, main, x, z));
                spinner = Part(MeshUtil.StellaOctangula(1.3f), glow, 2.6f);
                body.AddChild(spinner);
                return (body, 3.9f, 2.6f);
        }
    }

    /// <summary>英雄由几个基础几何体拼成，高约 1.3 m。</summary>
    Node3D HeroModel(string model)
    {
        var body = new Node3D();
        if (model == "falcon")
        {
            body.AddChild(Solid(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.38f, Height = 1f, RadialSegments = 4 }, 0.5f));
            body.AddChild(Solid(new SphereMesh { Radius = 0.18f, Height = 0.36f }, 1.15f));
            body.AddChild(Solid(new BoxMesh { Size = new Vector3(1.1f, 0.06f, 0.3f) }, 0.7f));
        }
        else
        {
            body.AddChild(Solid(new BoxMesh { Size = new Vector3(0.75f, 0.75f, 0.75f) }, 0.375f));
            var head = Solid(new BoxMesh { Size = new Vector3(0.45f, 0.45f, 0.45f) }, 1f);
            head.RotationDegrees = new Vector3(0, 45, 0);
            body.AddChild(head);
        }
        return body;
    }

    MeshInstance3D Solid(Mesh mesh, float y = 0f) =>
        new() { Mesh = mesh, MaterialOverride = _white, Position = new Vector3(0, y, 0) };

    static MeshInstance3D Part(Mesh mesh, Material material, float y) =>
        new() { Mesh = mesh, MaterialOverride = material, Position = new Vector3(0, y, 0) };

    /// <summary>底面在 bottom 高度、中心在 (x, z) 的长方体。</summary>
    static MeshInstance3D Box(float w, float h, float d, float bottom, Material material, float x = 0f, float z = 0f) =>
        new()
        {
            Mesh = new BoxMesh { Size = new Vector3(w, h, d) },
            MaterialOverride = material,
            Position = new Vector3(x, bottom + h / 2f, z),
        };

    static (float X, float Z)[] Corners(float offset) =>
        [(offset, offset), (offset, -offset), (-offset, offset), (-offset, -offset)];

    StandardMaterial3D Material(Color color, float emission = 0f)
    {
        var key = (color, emission);
        if (_materials.TryGetValue(key, out var material))
            return material;
        material = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f };
        if (emission > 0)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = emission;
        }
        return _materials[key] = material;
    }
}

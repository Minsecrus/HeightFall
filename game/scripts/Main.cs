using System.Globalization;
using System.Linq;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 场景入口：创建对局，搭建渲染、相机、操作和界面，按 30 Hz 推进逻辑。
/// 命令行参数（写在 -- 之后）：--screenshot=路径、--shot-frame=第几帧截图、--focus=x,y、--distance=米、
/// --advance=秒（开局先快进）、--pit=x,y（在该处挖一个坑）、
/// --cast=英雄序号,技能栏位,x,y（把英雄传送到目标西南方 3 格，升到 6 级、学会全部技能后对该格施法）、
/// --recall=英雄序号（开始回城读条）。
/// </summary>
public partial class Main : Node3D
{
    const Team Viewer = Team.Heights;

    World _world = null!;
    Unit[] _heroes = [];
    string? _screenshotPath;
    int _shotFrame = 20;
    int _frames;

    public override void _Ready()
    {
        _world = new World(StandardMap.Build());
        var map = _world.Map;
        Unit[] heroes = [_world.SpawnHero(Heroes.Bastion, Viewer), _world.SpawnHero(Heroes.Falcon, Viewer)];
        _heroes = heroes;
        // 敌方练习对象：守在中路一塔前，阵亡后会走回来
        _world.SpawnHero(Heroes.Falcon, Viewer.Opponent(), post: new GridPos(71, 71));

        var fountainTints = _world.Layout.Structures
            .Where(s => s.Kind == StructureKind.Fountain)
            .SelectMany(s => s.Footprint().Select(p => (Tile: p, Color: Palette.FountainTile(s))))
            .ToDictionary(t => t.Tile, t => t.Color);

        AddChild(CreateEnvironment());
        AddChild(CreateSun());
        var terrain = new MapView { Map = map, Tints = fountainTints };
        AddChild(terrain);
        var units = new UnitView { World = _world, Viewer = Viewer };
        AddChild(units);
        var flashes = new TileOverlay { Map = map };
        AddChild(flashes);
        var overlay = new TileOverlay { Map = map };
        AddChild(overlay);

        PlayerController? player = null;
        var rig = new CameraRig
        {
            Follow = () => WorldSpace.UnitPosition(map, player?.Hero ?? heroes[0], 0f),
            Bounds = new Rect2(0, -map.SizeY, map.SizeX, map.SizeY),
        };
        AddChild(rig);
        AddChild(new EffectsView
        {
            World = _world, Viewer = Viewer, Units = units, Terrain = terrain, CameraRig = rig, Flashes = flashes,
        });
        player = new PlayerController { World = _world, Camera = rig.Camera, Overlay = overlay, Viewer = Viewer, Heroes = heroes };
        AddChild(player);
        AddChild(new Hud { World = _world, Player = player, Viewer = Viewer });

        ApplyCommandLine(rig);
    }

    public override void _PhysicsProcess(double delta) => _world.Step();

    public override void _Process(double delta)
    {
        if (_screenshotPath is null || ++_frames < _shotFrame)
            return;
        GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
        GetTree().Quit();
    }

    void ApplyCommandLine(CameraRig rig)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var parts = arg.Split('=', 2);
            if (parts.Length != 2)
                continue;
            switch (parts[0])
            {
                case "--screenshot":
                    _screenshotPath = parts[1];
                    rig.EdgeScroll = false;
                    break;
                case "--shot-frame":
                    _shotFrame = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "--recall":
                    _world.OrderRecall(_heroes[int.Parse(parts[1], CultureInfo.InvariantCulture)]);
                    break;
                case "--cast":
                    var v = parts[1].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    var hero = _heroes[v[0]];
                    var target = new GridPos(v[2], v[3]);
                    _world.DebugTeleport(hero, target - new GridPos(3, 3));
                    for (var i = 1; i < 6; i++)
                        _world.DebugLevelUp(hero);
                    for (var slot = 0; slot < hero.Def.Skills.Count; slot++)
                        _world.LearnSkill(hero, slot);
                    _world.OrderCast(hero, v[1], target);
                    break;
                case "--focus":
                    var focus = ParsePoint(parts[1]);
                    rig.Locked = false;
                    rig.Focus(new Vector3(focus.X, WorldSpace.Elevation(HeightRules.Ground), -focus.Y));
                    break;
                case "--distance":
                    rig.SetDistance(float.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
                case "--advance":
                    for (var i = 0; i < SimTime.Ticks(float.Parse(parts[1], CultureInfo.InvariantCulture)); i++)
                        _world.Step();
                    _world.DrainEvents();
                    break;
                case "--pit":
                    var pit = ParsePoint(parts[1]);
                    foreach (var p in GridShapes.Circle(new GridPos((int)pit.X, (int)pit.Y), 2.5f))
                        _world.Map.AddHeightModifier(p, -3, SimTime.Ticks(30));
                    break;
            }
        }
    }

    static Vector2 ParsePoint(string text)
    {
        var xy = text.Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return new Vector2(xy[0], xy[1]);
    }

    static WorldEnvironment CreateEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Ground,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = 0.55f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            SsaoEnabled = true,
            SsaoRadius = 1f,
            SsaoIntensity = 0.8f,
        };
        return new WorldEnvironment { Environment = env };
    }

    /// <summary>主光从画面左上方（西北）照来，阴影浅而柔和。</summary>
    static DirectionalLight3D CreateSun()
    {
        var sun = new DirectionalLight3D
        {
            LightEnergy = 0.7f,
            ShadowEnabled = true,
            ShadowOpacity = 0.22f,
            ShadowBlur = 3f,
            DirectionalShadowMaxDistance = 80f,
        };
        sun.RotationDegrees = new Vector3(-55f, -135f, 0f);
        return sun;
    }
}

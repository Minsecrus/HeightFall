using System.Collections.Generic;
using System.Linq;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 地形渲染：每个格子是一根独立的方柱，柱与柱之间留一道窄缝，柱子一直延伸到地面下方的底板。
/// 顶面受光照、接收单位和建筑的影子；侧面和底板用不受光照的平涂色，按朝向固定变暗，
/// 所以缝隙再深也只是一道浅线。按 16×16 分块生成网格。格子高度变化时播放升降动画——
/// 升起 0.25 秒带回弹，下陷时砸下去再弹两下，效果结束后 0.5 秒缓慢回落；动画期间只重建受影响的分块。
/// </summary>
public partial class MapView : Node3D
{
    const int ChunkSize = 16;
    const float RiseSeconds = 0.25f;
    const float DropSeconds = 0.3f;
    const float RestoreSeconds = 0.5f;

    /// <summary>相邻两根柱子之间的缝隙（米）。</summary>
    const float Gap = 0.06f;

    /// <summary>柱子底部和底板的高度。</summary>
    /// <summary>柱子延伸到地面下 2 m 的底板。</summary>
    static readonly float FloorY = WorldSpace.Elevation(HeightRules.Ground) - 2f;

    static readonly Color FloorColor = new("#E6E8EA");

    /// <summary>侧面按朝向固定变暗的比例：主光来自西北，东、南两面最暗。</summary>
    static readonly (Vector3 Normal, float Shade)[] SideShades =
    [
        (Vector3.Right, 0.12f),
        (Vector3.Left, 0.05f),
        (Vector3.Forward, 0.03f),
        (Vector3.Back, 0.09f),
    ];

    enum AnimKind
    {
        Rise,
        Drop,
        Restore,
    }

    sealed class TileAnim(float from, float to, float duration, AnimKind kind)
    {
        public float From { get; } = from;
        public float To { get; } = to;
        public float Duration { get; } = duration;
        public AnimKind Kind { get; } = kind;
        public float Elapsed { get; set; }
    }

    readonly HashSet<Vector2I> _dirty = [];
    readonly Dictionary<GridPos, TileAnim> _anims = [];
    float[] _display = null!;
    MeshInstance3D[,] _chunks = null!;
    StandardMaterial3D _topMaterial = null!;
    StandardMaterial3D _sideMaterial = null!;

    public GameMap Map { get; set; } = null!;

    /// <summary>替换地表默认颜色的格子，例如泉水格。</summary>
    public IReadOnlyDictionary<GridPos, Color> Tints { get; set; } = new Dictionary<GridPos, Color>();

    public override void _Ready()
    {
        // 顶点色在 C# 中已转为线性空间
        _topMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
        _sideMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        _display = new float[Map.SizeX * Map.SizeY];
        foreach (var p in Map.AllTiles())
            _display[Index(p)] = Map.HeightAt(p);
        WorldSpace.TerrainElevation = ElevationAt;

        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(Map.SizeX, Map.SizeY) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = FloorColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
            Position = new Vector3(Map.SizeX / 2f, FloorY, -Map.SizeY / 2f),
        });

        var countX = (Map.SizeX + ChunkSize - 1) / ChunkSize;
        var countY = (Map.SizeY + ChunkSize - 1) / ChunkSize;
        _chunks = new MeshInstance3D[countX, countY];
        for (var cy = 0; cy < countY; cy++)
        for (var cx = 0; cx < countX; cx++)
        {
            var chunk = new MeshInstance3D();
            AddChild(chunk);
            _chunks[cx, cy] = chunk;
            Rebuild(new Vector2I(cx, cy));
        }

        Map.HeightChanged += OnHeightChanged;
    }

    public override void _ExitTree()
    {
        Map.HeightChanged -= OnHeightChanged;
        WorldSpace.TerrainElevation = null;
    }

    /// <summary>格子当前显示的顶面高度（米），包含升降动画。</summary>
    public float ElevationAt(GridPos p) =>
        Map.InBounds(p) ? _display[Index(p)] * WorldSpace.LevelHeight : 0f;

    /// <summary>推迟某格正在播放的升降动画，用于让一排格子依次隆起。</summary>
    public void DelayAnimation(GridPos p, float seconds)
    {
        if (_anims.TryGetValue(p, out var anim))
            anim.Elapsed = -seconds;
    }

    public override void _Process(double delta)
    {
        foreach (var (p, anim) in _anims.ToList())
        {
            anim.Elapsed += (float)delta;
            var t = Mathf.Clamp(anim.Elapsed / anim.Duration, 0f, 1f);
            _display[Index(p)] = Mathf.Lerp(anim.From, anim.To, Ease(anim.Kind, t));
            MarkDirty(p);
            if (t >= 1f)
                _anims.Remove(p);
        }

        foreach (var chunk in _dirty)
            Rebuild(chunk);
        _dirty.Clear();
    }

    void OnHeightChanged(GridPos p, int oldHeight, int newHeight, bool restoring)
    {
        var kind = restoring ? AnimKind.Restore
            : newHeight > oldHeight ? AnimKind.Rise
            : AnimKind.Drop;
        var duration = kind switch
        {
            AnimKind.Rise => RiseSeconds,
            AnimKind.Drop => DropSeconds,
            _ => RestoreSeconds,
        };
        _anims[p] = new TileAnim(_display[Index(p)], newHeight, duration, kind);
        MarkDirty(p);
    }

    /// <summary>升起：越过目标再回弹；下陷：砸下去弹两下；回落：平滑。</summary>
    static float Ease(AnimKind kind, float t)
    {
        switch (kind)
        {
            case AnimKind.Rise:
                const float c1 = 2.2f;
                var u = t - 1f;
                return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
            case AnimKind.Drop:
                const float n = 7.5625f, d = 2.75f;
                if (t < 1f / d)
                    return n * t * t;
                if (t < 2f / d)
                    return n * (t -= 1.5f / d) * t + 0.75f;
                if (t < 2.5f / d)
                    return n * (t -= 2.25f / d) * t + 0.9375f;
                return n * (t -= 2.625f / d) * t + 0.984375f;
            default:
                return t * t * (3f - 2f * t);
        }
    }

    void MarkDirty(GridPos p) => _dirty.Add(new Vector2I(p.X / ChunkSize, p.Y / ChunkSize));

    /// <summary>顶面和侧面分成两个面组，各用自己的材质。</summary>
    void Rebuild(Vector2I chunk)
    {
        var tops = new SurfaceTool();
        var sides = new SurfaceTool();
        tops.Begin(Mesh.PrimitiveType.Triangles);
        sides.Begin(Mesh.PrimitiveType.Triangles);
        for (var y = chunk.Y * ChunkSize; y < Mathf.Min((chunk.Y + 1) * ChunkSize, Map.SizeY); y++)
        for (var x = chunk.X * ChunkSize; x < Mathf.Min((chunk.X + 1) * ChunkSize, Map.SizeX); x++)
            AddPillar(tops, sides, new GridPos(x, y));

        var mesh = tops.Commit();
        sides.Commit(mesh);
        mesh.SurfaceSetMaterial(0, _topMaterial);
        mesh.SurfaceSetMaterial(1, _sideMaterial);
        _chunks[chunk.X, chunk.Y].Mesh = mesh;
    }

    /// <summary>一根从底板升到格子顶面的方柱，四周各缩进半道缝隙。</summary>
    void AddPillar(SurfaceTool tops, SurfaceTool sides, GridPos p)
    {
        var top = ElevationAt(p);
        var color = Tints.TryGetValue(p, out var tint) ? tint : Palette.ForGround(Map.KindAt(p), _display[Index(p)]);
        const float inset = Gap / 2;
        float x0 = p.X + inset, x1 = p.X + 1 - inset, z0 = -(p.Y + 1) + inset, z1 = -p.Y - inset;

        MeshUtil.AddQuad(tops,
            new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x0, top, z1),
            Vector3.Up, color.SrgbToLinear(), Vector2.Zero);
        foreach (var (normal, shade) in SideShades)
        {
            var (a, b) = normal.X > 0 ? (new Vector2(x1, z0), new Vector2(x1, z1))
                : normal.X < 0 ? (new Vector2(x0, z0), new Vector2(x0, z1))
                : normal.Z < 0 ? (new Vector2(x0, z0), new Vector2(x1, z0))
                : (new Vector2(x0, z1), new Vector2(x1, z1));
            MeshUtil.AddQuad(sides,
                new Vector3(a.X, top, a.Y), new Vector3(b.X, top, b.Y),
                new Vector3(b.X, FloorY, b.Y), new Vector3(a.X, FloorY, a.Y),
                normal, color.Darkened(shade).SrgbToLinear(), Vector2.Zero);
        }
    }

    int Index(GridPos p) => p.Y * Map.SizeX + p.X;
}

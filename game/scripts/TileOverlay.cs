using System.Collections.Generic;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>贴在格子顶面的半透明高亮，用于鼠标悬停、技能范围和延迟落点。每帧由调用方重新填充。</summary>
public partial class TileOverlay : MeshInstance3D
{
    const float Lift = 0.02f;
    const float Inset = 0.03f;

    readonly List<(GridPos Tile, Color Color)> _tiles = [];
    ImmediateMesh _mesh = null!;
    StandardMaterial3D _material = null!;

    public GameMap Map { get; set; } = null!;

    public override void _Ready()
    {
        _mesh = new ImmediateMesh();
        Mesh = _mesh;
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        CastShadow = ShadowCastingSetting.Off;
    }

    public void Clear() => _tiles.Clear();

    public void Add(GridPos tile, Color color)
    {
        if (Map.InBounds(tile))
            _tiles.Add((tile, color));
    }

    public void Add(IEnumerable<GridPos> tiles, Color color)
    {
        foreach (var t in tiles)
            Add(t, color);
    }

    public void Commit()
    {
        _mesh.ClearSurfaces();
        if (_tiles.Count == 0)
            return;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _material);
        foreach (var (tile, color) in _tiles)
        {
            var y = WorldSpace.GroundAt(Map, tile) + Lift;
            float x0 = tile.X + Inset, x1 = tile.X + 1 - Inset, z0 = -(tile.Y + 1) + Inset, z1 = -tile.Y - Inset;
            _mesh.SurfaceSetColor(color);
            foreach (var v in new[]
                     {
                         new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1),
                         new Vector3(x0, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1),
                     })
                _mesh.SurfaceAddVertex(v);
        }
        _mesh.SurfaceEnd();
    }
}

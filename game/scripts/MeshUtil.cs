using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HeightFall.Game;

public static class MeshUtil
{
    static readonly float Phi = (1f + Mathf.Sqrt(5f)) / 2f;

    /// <summary>添加一个朝向 normal 的面：自动调整顶点顺序，保证 Godot 的顺时针正面。</summary>
    public static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal, Color color, Vector2 uv)
    {
        AddTriangle(st, a, b, c, normal, color, uv);
        AddTriangle(st, a, c, d, normal, color, uv);
    }

    public static void AddTriangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c,
        Vector3 normal, Color color, Vector2 uv)
    {
        if ((b - a).Cross(c - a).Dot(normal) > 0)
            (b, c) = (c, b);
        foreach (var v in new[] { a, b, c })
        {
            st.SetNormal(normal);
            st.SetColor(color);
            st.SetUV(uv);
            st.AddVertex(v);
        }
    }

    public static ArrayMesh Tetrahedron(float radius) =>
        TriangleSolid([new(1, 1, 1), new(1, -1, -1), new(-1, 1, -1), new(-1, -1, 1)], radius);

    public static ArrayMesh Octahedron(float radius) =>
        TriangleSolid([Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward], radius);

    public static ArrayMesh Icosahedron(float radius) => TriangleSolid(IcosahedronVertices(), radius);

    /// <summary>正十二面体：取正二十面体的对偶，每个二十面体顶点对应一个五边形面。</summary>
    public static ArrayMesh Dodecahedron(float radius)
    {
        var ico = IcosahedronVertices().Select(v => v.Normalized()).ToArray();
        var faces = TriangleFaces(ico);
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var a = 0; a < ico.Length; a++)
        {
            var axis = ico[a];
            var ring = faces.Where(f => f.Contains(a))
                .Select(f => (ico[f[0]] + ico[f[1]] + ico[f[2]]).Normalized() * radius)
                .ToList();
            var right = (ring[0] - axis * ring[0].Dot(axis)).Normalized();
            var up = axis.Cross(right);
            ring.Sort((p, q) => Mathf.Atan2(p.Dot(up), p.Dot(right)).CompareTo(Mathf.Atan2(q.Dot(up), q.Dot(right))));
            var center = ring.Aggregate(Vector3.Zero, (sum, p) => sum + p) / ring.Count;
            for (var i = 0; i < ring.Count; i++)
                AddTriangle(st, center, ring[i], ring[(i + 1) % ring.Count], axis, Colors.White, Vector2.Zero);
        }
        return st.Commit();
    }

    /// <summary>星形八面体：两个正四面体交叠，顶点到中心距离为 radius。</summary>
    public static ArrayMesh StellaOctangula(float radius)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var s = radius / Mathf.Sqrt(3f);
        Vector3[] tetra = [new(s, s, s), new(s, -s, -s), new(-s, s, -s), new(-s, -s, s)];
        foreach (var sign in new[] { 1f, -1f })
        {
            for (var skip = 0; skip < 4; skip++)
            {
                var face = new Vector3[3];
                for (int i = 0, k = 0; i < 4; i++)
                {
                    if (i != skip)
                        face[k++] = tetra[i] * sign;
                }
                var normal = (face[0] + face[1] + face[2]).Normalized();
                AddTriangle(st, face[0], face[1], face[2], normal, Colors.White, Vector2.Zero);
            }
        }
        return st.Commit();
    }

    static Vector3[] IcosahedronVertices()
    {
        var list = new List<Vector3>();
        foreach (var a in new[] { -1f, 1f })
        foreach (var b in new[] { -Phi, Phi })
        {
            list.Add(new Vector3(0, a, b));
            list.Add(new Vector3(a, b, 0));
            list.Add(new Vector3(b, 0, a));
        }
        return list.ToArray();
    }

    /// <summary>面为三角形的正多面体：三条边都等于最短边长的顶点三元组就是一个面。</summary>
    static ArrayMesh TriangleSolid(Vector3[] vertices, float radius)
    {
        var v = vertices.Select(p => p.Normalized() * radius).ToArray();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var f in TriangleFaces(v))
        {
            var normal = (v[f[0]] + v[f[1]] + v[f[2]]).Normalized();
            AddTriangle(st, v[f[0]], v[f[1]], v[f[2]], normal, Colors.White, Vector2.Zero);
        }
        return st.Commit();
    }

    static List<int[]> TriangleFaces(Vector3[] v)
    {
        var edge = float.MaxValue;
        for (var i = 0; i < v.Length; i++)
        for (var j = i + 1; j < v.Length; j++)
            edge = Mathf.Min(edge, v[i].DistanceTo(v[j]));

        bool IsEdge(int i, int j) => Mathf.Abs(v[i].DistanceTo(v[j]) - edge) < edge * 0.01f;
        var faces = new List<int[]>();
        for (var i = 0; i < v.Length; i++)
        for (var j = i + 1; j < v.Length; j++)
        for (var k = j + 1; k < v.Length; k++)
        {
            if (IsEdge(i, j) && IsEdge(j, k) && IsEdge(i, k))
                faces.Add([i, j, k]);
        }
        return faces;
    }
}

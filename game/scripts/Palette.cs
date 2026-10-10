using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>docs/design.md 中的色板。</summary>
public static class Palette
{
    public static readonly Color Ground = new("#F5F5F3");
    public static readonly Color Unit = new("#FFFFFF");
    public static readonly Color Ally = new("#2E9CCA");
    public static readonly Color Enemy = new("#D9533B");
    public static readonly Color Neutral = new("#3C3C3C");
    public static readonly Color Mana = new("#7F97AD");
    public static readonly Color Gold = new("#E0B04A");

    /// <summary>坠落扬起的尘土、地形碎块。</summary>
    public static readonly Color Rock = new("#CFC8B7");

    /// <summary>受击闪光。</summary>
    public static readonly Color Hit = new("#FF8A5C");

    /// <summary>分层设色：每级高度一种颜色，低处偏冷、高处偏暖偏深，相差 1 级也能分清。</summary>
    static readonly Color[] HeightRamp =
    [
        new("#D3DDE6"), new("#E1E9EE"), new("#F5F5F3"), new("#ECEAE3"),
        new("#E3DFD4"), new("#D9D3C5"), new("#CFC8B7"), new("#C4BCA9"),
    ];

    /// <summary>兵线和坡道比同高度的地面略深一点。</summary>
    const float LaneShade = 0.035f;

    // 建筑按阵营上色：「岭」冷色，「渊」暖色；塔按 T1 → T4 由浅到深
    static readonly Color[] HeightsTowers = [new("#8ED1C6"), new("#5DB7C9"), new("#4B95C8"), new("#5872C4")];
    static readonly Color[] FallTowers = [new("#F0C27A"), new("#EB9A5C"), new("#DB6E4B"), new("#BF4F4A")];
    static readonly Color HeightsBarracks = new("#8A86C9");
    static readonly Color FallBarracks = new("#C76A8E");
    static readonly Color HeightsAncient = new("#6BC3E0");
    static readonly Color FallAncient = new("#F08A5D");

    /// <summary>地面颜色由高度决定（level 可以是升降动画中的小数），兵线和坡道略深。</summary>
    public static Color ForGround(TerrainKind kind, float level)
    {
        var clamped = Mathf.Clamp(level, 0f, HeightRamp.Length - 1);
        var low = Mathf.FloorToInt(clamped);
        var high = Mathf.Min(low + 1, HeightRamp.Length - 1);
        var color = HeightRamp[low].Lerp(HeightRamp[high], clamped - low);
        return kind is TerrainKind.Lane or TerrainKind.Ramp ? color.Darkened(LaneShade) : color;
    }

    /// <summary>血条和技能范围的颜色按观察者视角分配。</summary>
    public static Color ForTeam(Team team, Team viewer) =>
        team == Team.Neutral ? Neutral : team == viewer ? Ally : Enemy;

    public static Color ForStructure(Structure s)
    {
        var heights = s.Team == Team.Heights;
        return s.Kind switch
        {
            StructureKind.Tower => (heights ? HeightsTowers : FallTowers)[s.Tier - 1],
            StructureKind.Barracks => heights ? HeightsBarracks : FallBarracks,
            StructureKind.Ancient => heights ? HeightsAncient : FallAncient,
            _ => heights ? HeightsTowers[0] : FallTowers[0],
        };
    }

    public static Color FountainTile(Structure fountain) => Ground.Lerp(ForStructure(fountain), 0.3f);
}

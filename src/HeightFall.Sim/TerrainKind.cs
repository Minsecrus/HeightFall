namespace HeightFall.Sim;

/// <summary>格子的地表类型，决定显示颜色；通行与否只看高度。</summary>
public enum TerrainKind
{
    Ground,
    Lane,
    River,
    Ramp,
    Plateau,
    Rock,
    Border,
}

namespace HeightFall.Sim;

/// <summary>
/// 高度规则：高度取整数 0–15，每级 0.2 m。向上只能走高度差 ≤ 1 的格子，2 级以上上不去；向下任何落差都能走，
/// 落差 ≥ 2 级（走下高崖或脚下格子突然下沉）时坠落，落地眩晕并受到物理伤害。地形效果结束时的缓慢回落不算坠落。
/// 这些规则只看高度，与地形是谁改变的无关。
/// </summary>
public static class HeightRules
{
    public const int Min = 0;
    public const int Max = 15;

    /// <summary>向上能走的最大高度差。</summary>
    public const int MaxStep = 1;

    /// <summary>落差达到该值时坠落。</summary>
    public const int FallThreshold = 2;

    public const int River = 6;
    public const int Ground = 7;

    /// <summary>基地出口的坡道和野区台地。</summary>
    public const int Ramp = 8;

    public const int Plateau = 9;
    public const int Rock = 15;
    public const int Border = 15;

    public static int Clamp(int height) => Math.Clamp(height, Min, Max);

    /// <summary>坠落眩晕：落差 2 级 0.4 秒，每多 1 级加 0.3 秒，最多 1.2 秒。</summary>
    public static float FallStunSeconds(int drop) =>
        drop < FallThreshold ? 0f : MathF.Min(1.2f, 0.4f + 0.3f * (drop - FallThreshold));

    /// <summary>坠落伤害（物理，受物防减免）：落差 2 级 40 点，每多 1 级加 30 点。</summary>
    public static float FallDamage(int drop) =>
        drop < FallThreshold ? 0f : 40f + 30f * (drop - FallThreshold);
}

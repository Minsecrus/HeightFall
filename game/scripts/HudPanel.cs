using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 底部中央的英雄面板：等级圆（外圈是经验环，阵亡时显示复活倒计时）、血条、蓝条、6 格物品和金钱。
/// </summary>
public partial class HudPanel : Control
{
    public const float PanelWidth = 560f;
    public const float PanelHeight = 84f;

    const float LevelRadius = 26f;
    const float ItemSize = 28f;
    const float ItemGap = 4f;
    static readonly Vector2 LevelCenter = new(44, 42);
    static readonly Rect2 HpBar = new(86, 18, 300, 22);
    static readonly Rect2 ManaBar = new(86, 46, 300, 10);
    static readonly Vector2 ItemsOrigin = new(404, 12);

    static readonly Color Background = new(1f, 1f, 1f, 0.92f);
    static readonly Color Border = new("#D9D9D7");
    static readonly Color Track = new("#ECECEA");

    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var hero = Player.Hero;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(0, 0, PanelWidth, PanelHeight), Background);
        DrawRect(new Rect2(0, 0, PanelWidth, PanelHeight), Border, filled: false);

        DrawCircle(LevelCenter, LevelRadius, Colors.White);
        DrawArc(LevelCenter, LevelRadius, 0, Mathf.Tau, 48, Border, 1.5f, true);
        var xp = hero.Xp / (float)GameRules.XpToNext(hero.Level);
        DrawArc(LevelCenter, LevelRadius + 6, 0, Mathf.Tau, 48, Track, 4f, true);
        if (xp > 0)
            DrawArc(LevelCenter, LevelRadius + 6, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * xp, 48, Palette.Ally, 4f, true);
        var levelText = hero.IsDead ? $"{Mathf.CeilToInt(SimTime.Seconds(hero.RespawnTicks))}" : $"{hero.Level}";
        DrawString(font, LevelCenter + new Vector2(-LevelRadius, 8), levelText, HorizontalAlignment.Center, LevelRadius * 2, 22,
            hero.IsDead ? Palette.Enemy : Palette.Neutral);

        DrawBar(HpBar, hero.Hp / hero.MaxHp, Palette.Ally, $"{Mathf.CeilToInt(hero.Hp)} / {Mathf.CeilToInt(hero.MaxHp)}");
        DrawBar(ManaBar, hero.MaxMana > 0 ? hero.Mana / hero.MaxMana : 0, Palette.Mana, null);

        for (var row = 0; row < 2; row++)
        for (var col = 0; col < 3; col++)
        {
            var slot = new Rect2(ItemsOrigin + new Vector2(col * (ItemSize + ItemGap), row * (ItemSize + ItemGap)), ItemSize, ItemSize);
            DrawRect(slot, Border, filled: false);
        }

        var goldAt = new Vector2(ItemsOrigin.X + 3 * (ItemSize + ItemGap) + 12, LevelCenter.Y);
        DrawColoredPolygon(
        [
            goldAt + new Vector2(0, -7), goldAt + new Vector2(6, 0), goldAt + new Vector2(0, 7), goldAt + new Vector2(-6, 0),
        ], Palette.Gold);
        DrawString(font, goldAt + new Vector2(12, 6), $"{hero.Gold}", fontSize: 16, modulate: Palette.Neutral);
    }

    void DrawBar(Rect2 rect, float fraction, Color fill, string? label)
    {
        DrawRect(rect, Track);
        DrawRect(new Rect2(rect.Position, rect.Size.X * Mathf.Clamp(fraction, 0f, 1f), rect.Size.Y), fill);
        if (label is not null)
            DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(0, rect.Size.Y - 5), label,
                HorizontalAlignment.Center, rect.Size.X, 13, Colors.White);
    }
}

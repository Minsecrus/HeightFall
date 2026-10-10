using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 顶部信息：中央是双方击杀比分夹着游戏时间（己方在左）；右上角是当前英雄的击杀 / 阵亡和补刀 / 反补。
/// </summary>
public partial class TopBar : Control
{
    const float Height = 40f;

    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var width = GetViewportRect().Size.X;
        var mid = width / 2;

        var seconds = (int)World.Seconds;
        DrawString(font, new Vector2(mid - 40, 26), $"{seconds / 60:00}:{seconds % 60:00}", HorizontalAlignment.Center, 80, 18,
            Palette.Neutral);
        DrawScore(new Vector2(mid - 52, 26), World.TeamKills(Viewer), Palette.Ally, alignRight: true);
        DrawScore(new Vector2(mid + 52, 26), World.TeamKills(Viewer.Opponent()), Palette.Enemy, alignRight: false);

        var hero = Player.Hero;
        var x = width - 200;
        DrawCross(new Vector2(x, 20));
        DrawString(font, new Vector2(x + 12, 26), $"{hero.Kills} / {hero.Deaths}", fontSize: 15, modulate: Palette.Neutral);
        DrawRect(new Rect2(x + 86, 14, 12, 12), Palette.Neutral);
        DrawString(font, new Vector2(x + 106, 26), $"{hero.LastHits} / {hero.Denies}", fontSize: 15, modulate: Palette.Neutral);
    }

    /// <summary>击杀数紧贴中间的时间，队伍色小方块在外侧。</summary>
    void DrawScore(Vector2 anchor, int kills, Color color, bool alignRight)
    {
        const float boxSize = 10f;
        const float fontSize = 18;
        var font = ThemeDB.FallbackFont;
        var text = $"{kills}";
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, (int)fontSize).X;
        var x = alignRight ? anchor.X - width : anchor.X;
        DrawString(font, new Vector2(x, anchor.Y), text, fontSize: (int)fontSize, modulate: Palette.Neutral);
        var boxX = alignRight ? x - 8 - boxSize : x + width + 8;
        DrawRect(new Rect2(boxX, anchor.Y - 12, boxSize, boxSize), color);
    }

    void DrawCross(Vector2 c)
    {
        const float s = 6f;
        DrawLine(c + new Vector2(-s, -s), c + new Vector2(s, s), Palette.Neutral, 2.5f, true);
        DrawLine(c + new Vector2(-s, s), c + new Vector2(s, -s), Palette.Neutral, 2.5f, true);
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.TopWide);
        OffsetBottom = Height;
        MouseFilter = MouseFilterEnum.Ignore;
    }
}

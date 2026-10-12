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
    public bool TouchMode { get; set; }
    public Vector4 SafeInsets { get; set; }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var width = GetViewportRect().Size.X;
        var mid = width / 2;
        var y = TouchMode ? SafeInsets.Y : 0;
        DrawSetTransform(new Vector2(0, y));

        var seconds = (int)World.Seconds;
        var timeSize = TouchMode ? 26 : 18;
        var baseline = TouchMode ? 32 : 26;
        DrawString(font, new Vector2(mid - (TouchMode ? 46 : 40), baseline), $"{seconds / 60:00}:{seconds % 60:00}", HorizontalAlignment.Center, TouchMode ? 92 : 80, timeSize,
            Palette.Neutral);
        DrawScore(new Vector2(mid - (TouchMode ? 64 : 52), baseline), World.TeamKills(Viewer), Palette.Ally, alignRight: true);
        DrawScore(new Vector2(mid + (TouchMode ? 64 : 52), baseline), World.TeamKills(Viewer.Opponent()), Palette.Enemy, alignRight: false);

        var hero = Player.Hero;
        var x = width - (TouchMode ? 278 : 200) - (TouchMode ? SafeInsets.Z : 0);
        DrawCross(new Vector2(x, 20));
        DrawString(font, new Vector2(x + 12, baseline), $"{hero.Kills} / {hero.Deaths}", fontSize: TouchMode ? 24 : 15, modulate: Palette.Neutral);
        var lastHitX = TouchMode ? 138 : 86;
        DrawRect(new Rect2(x + lastHitX, 14, 12, 12), Palette.Neutral);
        DrawString(font, new Vector2(x + lastHitX + 20, baseline), $"{hero.LastHits} / {hero.Denies}", fontSize: TouchMode ? 24 : 15, modulate: Palette.Neutral);

        if (World.Winner is { } winner)
            DrawResult(winner == Viewer, width, GetViewportRect().Size.Y - y);
    }

    /// <summary>基地核心被摧毁后，画面中央显示胜负，对局停止推进。</summary>
    void DrawResult(bool victory, float width, float height)
    {
        var font = ThemeDB.FallbackFont;
        var size = new Vector2(TouchMode ? 360 : 300, TouchMode ? 120 : 100);
        var box = new Rect2(new Vector2(width, height) / 2 - size / 2, size);
        DrawRect(box, Colors.White);
        DrawRect(box, victory ? Palette.Ally : Palette.Enemy, filled: false, width: 2f);
        DrawString(font, new Vector2(box.Position.X, box.GetCenter().Y + (TouchMode ? 18 : 15)), victory ? "胜利" : "失败",
            HorizontalAlignment.Center, box.Size.X, TouchMode ? 52 : 44, victory ? Palette.Ally : Palette.Enemy);
    }

    /// <summary>击杀数紧贴中间的时间，队伍色小方块在外侧。</summary>
    void DrawScore(Vector2 anchor, int kills, Color color, bool alignRight)
    {
        const float boxSize = 10f;
        var fontSize = TouchMode ? 26 : 18;
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

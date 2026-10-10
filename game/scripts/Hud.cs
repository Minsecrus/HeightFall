using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 界面层，布局参照主流 MOBA：顶部中央比分和时间，右上角个人战绩，底部中央英雄面板，右下角技能按钮，
/// 左下角留给小地图。
/// </summary>
public partial class Hud : CanvasLayer
{
    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;
    public Team Viewer { get; set; } = Team.Heights;

    public override void _Ready()
    {
        AddChild(new TopBar { World = World, Player = Player, Viewer = Viewer });

        var panel = new HudPanel { World = World, Player = Player };
        AddChild(panel);
        Place(panel, Control.LayoutPreset.CenterBottom,
            -HudPanel.PanelWidth / 2, -HudPanel.PanelHeight - 12, HudPanel.PanelWidth / 2, -12);

        var skills = new SkillBar { World = World, Player = Player };
        AddChild(skills);
        Place(skills, Control.LayoutPreset.BottomRight, -SkillBar.BarWidth - 12, -SkillBar.BarHeight - 8, -12, -8);
    }

    /// <summary>按锚点预设放置控件，偏移量相对锚点。</summary>
    static void Place(Control control, Control.LayoutPreset anchors, float left, float top, float right, float bottom)
    {
        control.SetAnchorsPreset(anchors);
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }
}

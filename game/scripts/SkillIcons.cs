using Godot;

namespace HeightFall.Game;

/// <summary>技能按钮里的线条几何图标，按 SkillDef.Icon 选择。size 是图标的半宽。</summary>
public static class SkillIcons
{
    const float Width = 3f;

    public static void Draw(CanvasItem canvas, string icon, Vector2 c, float size, Color color)
    {
        var s = size;
        switch (icon)
        {
            case "crack":
                // 地面裂开的折线
                canvas.DrawPolyline(
                [
                    c + new Vector2(-s, 0.3f * s), c + new Vector2(-0.5f * s, -0.3f * s), c + new Vector2(0, 0.3f * s),
                    c + new Vector2(0.5f * s, -0.3f * s), c + new Vector2(s, 0.3f * s),
                ], color, Width, true);
                break;
            case "raise":
                Columns(canvas, c, s, [0.4f, 1f, 0.4f], color);
                break;
            case "lower":
                Columns(canvas, c, s, [0.9f, 0.2f, 0.9f], color);
                break;
            case "cliff":
                Columns(canvas, c, s, [1f, 1f, 1f, 1f, 1f], color);
                break;
            case "arrow":
                Arrow(canvas, c + new Vector2(-s, 0), c + new Vector2(s, 0), s, color);
                break;
            case "blink":
                // 虚线 + 落点
                for (var i = 0; i < 3; i++)
                {
                    var x = -s + i * 0.45f * s;
                    canvas.DrawLine(c + new Vector2(x, 0), c + new Vector2(x + 0.25f * s, 0), color, Width, true);
                }
                canvas.DrawCircle(c + new Vector2(0.75f * s, 0), 0.25f * s, color);
                break;
            case "leap":
                // 跃起的弧线
                canvas.DrawArc(c + new Vector2(0, 0.4f * s), 0.85f * s, Mathf.Pi, Mathf.Tau, 24, color, Width, true);
                canvas.DrawLine(c + new Vector2(0.85f * s, 0.4f * s), c + new Vector2(0.55f * s, 0.05f * s), color, Width, true);
                canvas.DrawLine(c + new Vector2(0.85f * s, 0.4f * s), c + new Vector2(1.05f * s, 0.05f * s), color, Width, true);
                break;
            case "pit":
                // 向下的箭头落进坑里
                Arrow(canvas, c + new Vector2(0, -s), c + new Vector2(0, 0.15f * s), 0.8f * s, color);
                canvas.DrawPolyline(
                [
                    c + new Vector2(-0.8f * s, 0.35f * s), c + new Vector2(-0.8f * s, 0.85f * s),
                    c + new Vector2(0.8f * s, 0.85f * s), c + new Vector2(0.8f * s, 0.35f * s),
                ], color, Width, true);
                break;
            case "recall":
                // 房子：三角屋顶 + 方形墙
                canvas.DrawPolyline(
                [
                    c + new Vector2(-0.9f * s, -0.05f * s), c + new Vector2(0, -0.9f * s), c + new Vector2(0.9f * s, -0.05f * s),
                ], color, Width, true);
                canvas.DrawPolyline(
                [
                    c + new Vector2(-0.6f * s, -0.2f * s), c + new Vector2(-0.6f * s, 0.8f * s),
                    c + new Vector2(0.6f * s, 0.8f * s), c + new Vector2(0.6f * s, -0.2f * s),
                ], color, Width, true);
                break;
            case "restore":
                // 三道逐渐变长的横杠，表示慢慢回满
                for (var i = 0; i < 3; i++)
                {
                    var w = (0.4f + 0.3f * i) * s;
                    canvas.DrawRect(new Rect2(c.X - w, c.Y + (0.55f - 0.55f * i) * s - 0.12f * s, 2 * w, 0.24f * s), color);
                }
                break;
            case "heal":
                canvas.DrawRect(new Rect2(c.X - 0.2f * s, c.Y - 0.85f * s, 0.4f * s, 1.7f * s), color);
                canvas.DrawRect(new Rect2(c.X - 0.85f * s, c.Y - 0.2f * s, 1.7f * s, 0.4f * s), color);
                break;
            case "purify":
                // 菱形护盾
                canvas.DrawPolyline(
                [
                    c + new Vector2(0, -0.9f * s), c + new Vector2(0.8f * s, 0), c + new Vector2(0, 0.9f * s),
                    c + new Vector2(-0.8f * s, 0), c + new Vector2(0, -0.9f * s),
                ], color, Width, true);
                canvas.DrawCircle(c, 0.22f * s, color);
                break;
            case "sprint":
                // 两个向右的 V 形
                foreach (var dx in new[] { -0.45f, 0.25f })
                {
                    canvas.DrawPolyline(
                    [
                        c + new Vector2((dx - 0.3f) * s, -0.7f * s), c + new Vector2((dx + 0.3f) * s, 0),
                        c + new Vector2((dx - 0.3f) * s, 0.7f * s),
                    ], color, Width, true);
                }
                break;
            case "smite":
                // 闪电折线
                canvas.DrawPolyline(
                [
                    c + new Vector2(0.3f * s, -0.95f * s), c + new Vector2(-0.35f * s, 0.05f * s), c + new Vector2(0.3f * s, 0.05f * s),
                    c + new Vector2(-0.3f * s, 0.95f * s),
                ], color, Width, true);
                break;
            case "daze":
                // 向外放射的八条短线
                for (var i = 0; i < 8; i++)
                {
                    var dir = Vector2.FromAngle(Mathf.Tau * i / 8);
                    canvas.DrawLine(c + dir * 0.35f * s, c + dir * 0.9f * s, color, Width, true);
                }
                break;
            case "spark":
                // 四角星：法攻装备
                Star(canvas, c, 0.95f * s, color);
                break;
            case "ward":
                // 圆形护罩里一颗小四角星：法防装备
                canvas.DrawArc(c, 0.85f * s, 0, Mathf.Tau, 32, color, Width, true);
                Star(canvas, c, 0.45f * s, color);
                break;
            default:
                canvas.DrawCircle(c, 0.3f * s, color);
                break;
        }
    }

    /// <summary>一排从底部长出的方柱，heights 为相对 size 的高度。</summary>
    static void Columns(CanvasItem canvas, Vector2 c, float s, float[] heights, Color color)
    {
        var gap = 0.12f * s;
        var width = (2 * s - gap * (heights.Length - 1)) / heights.Length;
        for (var i = 0; i < heights.Length; i++)
        {
            var h = heights[i] * 1.6f * s;
            var x = c.X - s + i * (width + gap);
            canvas.DrawRect(new Rect2(x, c.Y + 0.8f * s - h, width, h), color);
        }
    }

    static void Arrow(CanvasItem canvas, Vector2 from, Vector2 to, float s, Color color)
    {
        canvas.DrawLine(from, to, color, Width, true);
        var dir = (to - from).Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        canvas.DrawLine(to, to - dir * 0.45f * s + side * 0.35f * s, color, Width, true);
        canvas.DrawLine(to, to - dir * 0.45f * s - side * 0.35f * s, color, Width, true);
    }

    /// <summary>四个尖角的星形轮廓，r 是尖角到中心的距离。</summary>
    static void Star(CanvasItem canvas, Vector2 c, float r, Color color)
    {
        var points = new Vector2[9];
        for (var i = 0; i < 8; i++)
            points[i] = c + Vector2.FromAngle(Mathf.Tau * i / 8 - Mathf.Pi / 2) * (i % 2 == 0 ? r : 0.32f * r);
        points[8] = points[0];
        canvas.DrawPolyline(points, color, Width, true);
    }
}

using System.Globalization;
using Godot;

namespace HeightFall.Game;

/// <summary>触控平台信息；游戏控件和逻辑仍由 Godot 绘制和执行。</summary>
public static class TouchPlatform
{
    // Godot's web input currently maps touchcancel to a normal release. The host
    // increments this counter before Godot receives that event so aiming can cancel.
    public static long ReadCancelSerial()
    {
        if (!OS.HasFeature("web"))
            return 0;
        return (long)JavaScriptBridge.Eval("Number(window.__heightFallTouchCancelSerial || 0)").AsDouble();
    }

    public static Vector4 ReadWebSafeInsets(Vector2 viewport)
    {
        if (!OS.HasFeature("web"))
            return Vector4.Zero;
        var raw = JavaScriptBridge.Eval("(() => { const s = getComputedStyle(document.documentElement); const c = document.getElementById('canvas').getBoundingClientRect(); return ['left','top','right','bottom'].map((x,i) => (parseFloat(s.getPropertyValue('--safe-area-' + x)) || 0) / (i % 2 ? c.height : c.width)).join(','); })()").AsString();
        var parts = raw.Split(',');
        if (parts.Length != 4)
            return Vector4.Zero;
        var values = new float[4];
        for (var i = 0; i < 4; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return Vector4.Zero;
        return new Vector4(values[0] * viewport.X, values[1] * viewport.Y,
            values[2] * viewport.X, values[3] * viewport.Y);
    }
}

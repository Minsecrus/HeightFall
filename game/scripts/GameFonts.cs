using System;
using Godot;

namespace HeightFall.Game;

/// <summary>Bundle Chinese glyphs without replacing Godot's original Latin font.</summary>
public static class GameFonts
{
    static FontFile? _chinese;

    /// <summary>Call before creating the game's UI, on both desktop and Web.</summary>
    public static void Install()
    {
        if (_chinese is not null)
            return;

        var chinese = GD.Load<FontFile>("res://fonts/HeightFallCjk-Regular.otf")
            ?? throw new InvalidOperationException("The bundled Chinese UI font could not be loaded.");
        var primary = ThemeDB.FallbackFont;
        var fallbacks = new Godot.Collections.Array<Font>();
        foreach (var fallback in primary.Fallbacks)
            fallbacks.Add(fallback);
        fallbacks.Add(chinese);
        // Assign the property so Godot also invalidates its cached fallback RIDs.
        primary.Fallbacks = fallbacks;
        _chinese = chinese;
    }
}

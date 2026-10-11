#if TWODOG_WEB_BOOT
using System;
using Godot.NativeInterop;

namespace GodotPlugins.Game
{
    internal static partial class Main
    {
        internal static unsafe IntPtr TwoDogGetInitializePointer() =>
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, int, godot_bool>)&InitializeFromGameProject;
    }
}

public static class TwoDogWebBoot
{
    public static IntPtr PluginsInitializer() => GodotPlugins.Game.Main.TwoDogGetInitializePointer();
}
#endif

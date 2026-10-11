using Godot;
using Engine = twodog.Engine;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Engine.RegisterWebPluginsInitializer(TwoDogWebBoot.PluginsInitializer());
        var engine = new Engine("HeightFall", args: args);
        try
        {
            engine.Start();
            GD.Print("HeightFall: Godot WebGPU host, original scene and C# scripts.");
            engine.Run();
        }
        catch
        {
            await engine.DisposeAsync();
            throw;
        }

        return 0;
    }
}

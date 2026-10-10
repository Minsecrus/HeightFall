namespace HeightFall.Sim;

public static class SimTime
{
    public const int TicksPerSecond = 30;

    public static int Ticks(float seconds) => (int)MathF.Round(seconds * TicksPerSecond);

    public static float Seconds(int ticks) => ticks / (float)TicksPerSecond;
}

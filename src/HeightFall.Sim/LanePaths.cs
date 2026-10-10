namespace HeightFall.Sim;

/// <summary>三条兵线的路径点：从「岭」方兵营门前到「渊」方兵营门前，「渊」方小兵逆向行走。</summary>
public static class LanePaths
{
    const int Spacing = 10;

    static readonly Dictionary<Lane, GridPos[]> Corners = new()
    {
        [Lane.Top] = [new(11, 29), new(11, 116), new(98, 116)],
        [Lane.Mid] = [new(28, 28), new(99, 99)],
        [Lane.Bot] = [new(29, 11), new(116, 11), new(116, 98)],
    };

    /// <summary>某方小兵在某路的路径点，最后一个点是敌方基地核心。</summary>
    public static IReadOnlyList<GridPos> For(Team team, Lane lane, GridPos enemyAncient)
    {
        var corners = Corners[lane];
        if (team == Team.Fall)
            corners = corners.Reverse().ToArray();

        var points = new List<GridPos> { corners[0] };
        for (var i = 1; i < corners.Length; i++)
        {
            var a = corners[i - 1];
            var b = corners[i];
            var segments = (int)MathF.Ceiling(a.DistanceTo(b) / Spacing);
            for (var s = 1; s <= segments; s++)
            {
                var t = s / (float)segments;
                points.Add(new GridPos((int)MathF.Round(a.X + (b.X - a.X) * t), (int)MathF.Round(a.Y + (b.Y - a.Y) * t)));
            }
        }
        points.Add(enemyAncient);
        return points;
    }
}

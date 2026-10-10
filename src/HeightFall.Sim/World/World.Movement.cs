namespace HeightFall.Sim;

public sealed partial class World
{
    const int MaxWaitTicks = 8;
    const int RetryTicks = 15;
    const float NearbyUnitRadius = 6f;

    enum MoveResult
    {
        Moving,
        Arrived,
        Stuck,
    }

    /// <summary>朝 goal 走，直到与它的距离 ≤ stopDistance。</summary>
    MoveResult MoveToward(Unit u, GridPos goal, float stopDistance)
    {
        if (DistanceToTile(u, goal) <= stopDistance)
        {
            ClearPath(u);
            return MoveResult.Arrived;
        }

        if (u.Path is null || u.PathGoal != goal || u.PathIndex >= u.Path.Count)
        {
            if (Tick < u.NextRepathTick)
                return MoveResult.Stuck;
            Repath(u, goal, avoidAllUnits: false);
            if (u.Path!.Count == 0)
            {
                u.NextRepathTick = Tick + RetryTicks;
                return MoveResult.Stuck;
            }
        }

        TryStep(u);
        return MoveResult.Moving;
    }

    /// <summary>沿路径迈一步。下一格被占时等待，等太久就绕开附近所有单位重新寻路。</summary>
    void TryStep(Unit u)
    {
        var next = u.Path![u.PathIndex];
        if (!Map.CanStep(u.Pos, next))
        {
            u.Path = null;
            return;
        }

        var occupant = UnitAt(next);
        if (occupant is not null && occupant != u)
        {
            if (++u.WaitTicks > MaxWaitTicks)
            {
                Repath(u, u.PathGoal, avoidAllUnits: true);
                u.WaitTicks = 0;
            }
            return;
        }

        u.WaitTicks = 0;
        var fromHeight = Map.HeightAt(u.Pos);
        var toHeight = Map.HeightAt(next);
        Vacate(u);
        u.PrevPos = u.Pos;
        u.Pos = next;
        u.PathIndex++;
        Occupy(u);
        var distance = next.X != u.PrevPos.X && next.Y != u.PrevPos.Y ? MathF.Sqrt(2f) : 1f;
        u.StepTicks = u.StepTicksLeft = Math.Max(1, (int)MathF.Round(distance / u.MoveSpeed * SimTime.TicksPerSecond));
        u.PendingFall = fromHeight - toHeight >= HeightRules.FallThreshold ? fromHeight - toHeight : 0;
    }

    /// <summary>
    /// 默认只把建筑和停着不动的单位当作障碍；avoidAllUnits 时附近所有单位都算障碍，终点被占也不再强行走过去。
    /// </summary>
    void Repath(Unit u, GridPos goal, bool avoidAllUnits)
    {
        Func<GridPos, bool> blocked = avoidAllUnits
            ? p => UnitAt(p) is { } o && o != u && (o.IsStructure || u.Pos.DistanceTo(p) <= NearbyUnitRadius)
            : p => UnitAt(p) is { } o && o != u && (o.IsStructure || (!o.IsStepping && o.Path is null));
        u.Path = _pathfinder.Find(u.Pos, goal, blocked, allowBlockedGoal: !avoidAllUnits);
        u.PathIndex = 0;
        u.PathGoal = goal;
    }

    static void ClearPath(Unit u)
    {
        u.Path = null;
        u.PathIndex = 0;
        u.WaitTicks = 0;
        u.NextRepathTick = 0;
    }
}

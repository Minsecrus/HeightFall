namespace HeightFall.Sim;

/// <summary>
/// 格子上的 A* 寻路，按 GameMap.CanStep 的方向规则走（高 2 级以上上不去，往下都能走）。
/// 坠落眩晕按时间折算成格数计入代价，所以平时绕坡道，只有直接跳下去明显更快时才跳。
/// </summary>
internal sealed class Pathfinder
{
    static readonly float Sqrt2 = MathF.Sqrt(2f);

    /// <summary>把秒折算成格数时假设的移速。</summary>
    const float TilesPerSecond = 3f;

    /// <summary>
    /// 终点被占时注定到不了，只需找离它最近的格子；最多展开这么多格，免得搜遍整张图。
    /// 网页版的 C# 在 wasm 解释器里运行，一次全图搜索会让手机卡顿几百毫秒。
    /// </summary>
    const int BlockedGoalSearchLimit = 1024;

    readonly GameMap _map;
    readonly float[] _cost;
    readonly int[] _parent;
    readonly int[] _seen;
    readonly int[] _closed;
    readonly PriorityQueue<int, float> _open = new();
    int _stamp;

    public Pathfinder(GameMap map)
    {
        _map = map;
        var count = map.SizeX * map.SizeY;
        _cost = new float[count];
        _parent = new int[count];
        _seen = new int[count];
        _closed = new int[count];
    }

    /// <summary>
    /// 返回从起点（不含）到终点的格子序列；终点不可达时，走到离终点最近的可达格。
    /// allowBlockedGoal 为 true 时，终点即使被占也视为可达（用于走到目标旁边）。
    /// </summary>
    public List<GridPos> Find(GridPos start, GridPos goal, Func<GridPos, bool> blocked, bool allowBlockedGoal)
    {
        _stamp++;
        _open.Clear();
        var startIndex = Index(start);
        var goalIndex = _map.InBounds(goal) ? Index(goal) : -1;
        _cost[startIndex] = 0;
        _parent[startIndex] = -1;
        _seen[startIndex] = _stamp;
        _open.Enqueue(startIndex, Heuristic(start, goal));

        var best = startIndex;
        var bestH = Heuristic(start, goal);
        var goalBlocked = goalIndex >= 0 && !allowBlockedGoal && blocked(goal);
        var expanded = 0;
        while (_open.TryDequeue(out var current, out _))
        {
            if (_closed[current] == _stamp)
                continue;
            _closed[current] = _stamp;
            var p = PosOf(current);
            var h = Heuristic(p, goal);
            if (h < bestH)
            {
                bestH = h;
                best = current;
            }
            if (current == goalIndex)
                break;
            // 被占的终点不会出队；与它正交相邻（h = 1）的格子已是最近，继续搜索也不会更好。
            if (goalBlocked && (h <= 1f || ++expanded >= BlockedGoalSearchLimit))
                break;

            foreach (var d in GridPos.Neighbors8)
            {
                var n = p + d;
                if (!_map.InBounds(n))
                    continue;
                var ni = Index(n);
                if (_closed[ni] == _stamp)
                    continue;
                if (!(allowBlockedGoal && ni == goalIndex) && blocked(n))
                    continue;
                if (!_map.CanStep(p, n))
                    continue;
                var cost = _cost[current] + (d.X != 0 && d.Y != 0 ? Sqrt2 : 1f) + HeightCost(p, n);
                if (_seen[ni] == _stamp && cost >= _cost[ni])
                    continue;
                _seen[ni] = _stamp;
                _cost[ni] = cost;
                _parent[ni] = current;
                _open.Enqueue(ni, cost + Heuristic(n, goal));
            }
        }

        var end = goalIndex >= 0 && _closed[goalIndex] == _stamp ? goalIndex : best;
        var path = new List<GridPos>();
        for (var i = end; i != startIndex; i = _parent[i])
            path.Add(PosOf(i));
        path.Reverse();
        return path;
    }

    float HeightCost(GridPos from, GridPos to) =>
        HeightRules.FallStunSeconds(_map.HeightAt(from) - _map.HeightAt(to)) * TilesPerSecond;

    static float Heuristic(GridPos a, GridPos b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return dx + dy + (Sqrt2 - 2f) * Math.Min(dx, dy);
    }

    int Index(GridPos p) => p.Y * _map.SizeX + p.X;

    GridPos PosOf(int index) => new(index % _map.SizeX, index / _map.SizeX);
}

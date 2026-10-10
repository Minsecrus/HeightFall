namespace HeightFall.Sim;

/// <summary>
/// 地图格子数据：基础高度、地表类型、技能产生的临时高度修正。
/// 当前高度 = 基础高度 + 该格所有修正值之和，限制在 0–7。高度规则见 HeightRules。
/// </summary>
public sealed class GameMap
{
    readonly int[] _baseHeight;
    readonly int[] _offset;
    readonly int[] _height;
    readonly TerrainKind[] _kind;
    readonly bool[] _fixed;
    readonly List<HeightModifier> _modifiers = [];

    public GameMap(int sizeX, int sizeY)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        var count = sizeX * sizeY;
        _baseHeight = new int[count];
        _offset = new int[count];
        _height = new int[count];
        _kind = new TerrainKind[count];
        _fixed = new bool[count];
    }

    public int SizeX { get; }
    public int SizeY { get; }

    /// <summary>
    /// 格子当前高度变化时触发，参数为位置、旧高度、新高度，以及是否为地形效果到期后的缓慢回落
    /// （回落不算坠落）。
    /// </summary>
    public event Action<GridPos, int, int, bool>? HeightChanged;

    public bool InBounds(GridPos p) => (uint)p.X < (uint)SizeX && (uint)p.Y < (uint)SizeY;

    public int HeightAt(GridPos p) => _height[Index(p)];

    public int BaseHeightAt(GridPos p) => _baseHeight[Index(p)];

    public TerrainKind KindAt(GridPos p) => _kind[Index(p)];

    /// <summary>建筑占地格和泉水区域的高度固定，不接受修正。</summary>
    public bool IsFixed(GridPos p) => _fixed[Index(p)];

    public IEnumerable<GridPos> AllTiles()
    {
        for (var y = 0; y < SizeY; y++)
        for (var x = 0; x < SizeX; x++)
            yield return new GridPos(x, y);
    }

    public void SetTerrain(GridPos p, TerrainKind kind, int baseHeight)
    {
        var i = Index(p);
        _kind[i] = kind;
        _baseHeight[i] = baseHeight;
        _height[i] = HeightRules.Clamp(baseHeight + _offset[i]);
    }

    public void SetFixed(GridPos p) => _fixed[Index(p)] = true;

    /// <summary>
    /// 能否从一格走到相邻格（有方向）：向上高度差 ≤ 1 才能走，向下任何落差都能走。
    /// 岩壁和边界只是足够高，从地面自然上不去。斜走时，两侧的正交格也都必须能从起点走进去，否则禁止斜穿。
    /// </summary>
    public bool CanStep(GridPos from, GridPos to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if ((dx == 0 && dy == 0) || Math.Abs(dx) > 1 || Math.Abs(dy) > 1)
            return false;
        if (!InBounds(from) || !CanEnter(from, to))
            return false;
        return dx == 0 || dy == 0
            || (CanEnter(from, new GridPos(to.X, from.Y)) && CanEnter(from, new GridPos(from.X, to.Y)));
    }

    bool CanEnter(GridPos from, GridPos to) =>
        InBounds(to) && HeightAt(to) - HeightAt(from) <= HeightRules.MaxStep;

    /// <summary>给一格加上带持续时间的高度修正。固定格或越界时返回 false。</summary>
    public bool AddHeightModifier(GridPos p, int delta, int durationTicks)
    {
        if (!InBounds(p) || IsFixed(p) || durationTicks <= 0)
            return false;
        _modifiers.Add(new HeightModifier(p, delta, durationTicks));
        ApplyOffset(p, delta, restoring: false);
        return true;
    }

    /// <summary>推进一个逻辑帧：到期的修正逐个撤销。</summary>
    public void Tick()
    {
        for (var i = _modifiers.Count - 1; i >= 0; i--)
        {
            var m = _modifiers[i];
            if (--m.RemainingTicks > 0)
                continue;
            _modifiers.RemoveAt(i);
            ApplyOffset(m.Pos, -m.Delta, restoring: true);
        }
    }

    void ApplyOffset(GridPos p, int delta, bool restoring)
    {
        var i = Index(p);
        _offset[i] += delta;
        var old = _height[i];
        _height[i] = HeightRules.Clamp(_baseHeight[i] + _offset[i]);
        if (_height[i] != old)
            HeightChanged?.Invoke(p, old, _height[i], restoring);
    }

    int Index(GridPos p) => p.Y * SizeX + p.X;

    sealed class HeightModifier(GridPos pos, int delta, int remainingTicks)
    {
        public GridPos Pos { get; } = pos;
        public int Delta { get; } = delta;
        public int RemainingTicks { get; set; } = remainingTicks;
    }
}

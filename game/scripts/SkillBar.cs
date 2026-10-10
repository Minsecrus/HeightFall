using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>
/// 右下角的圆形按钮：小号的回城（B）、恢复（V），中号的召唤师技能（F），以及 Q W E R。
/// 按钮内是几何图标，左下角标出按键；英雄技能外圈分段圆环显示等级，有技能点时右上角出现「+」（点击或按 1–4 学习）；
/// 冷却时按扇形遮罩倒计时，回城和恢复进行中外圈显示进度；放不了时外圈红色闪一下。
/// 点击按钮等同按键。站在己方泉水里点召唤师按钮，会在上方弹出一排召唤师技能供选择。
/// </summary>
public partial class SkillBar : Control
{
    public const float BarWidth = 470f;
    public const float BarHeight = 156f;

    /// <summary>回城、恢复按钮的编号，接在技能栏位之后。</summary>
    public const int RecallButton = 5;
    public const int RestoreButton = 6;

    const float RingGap = 0.2f;
    const float RingWidth = 4f;
    const float BadgeRadius = 11f;
    const float PickerRadius = 20f;
    const double RejectFlashSeconds = 0.3;
    static readonly string[] Keys = ["Q", "W", "E", "R", "F", "B", "V"];

    static readonly Color Border = new("#D9D9D7");
    static readonly Color Muted = new("#A0A09E");
    static readonly Color CooldownShade = new(0.24f, 0.24f, 0.24f, 0.35f);

    readonly double[] _rejectLeft = new double[Keys.Length];
    bool _pickerOpen;

    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;

    public override void _Ready() => Player.CastRejected += OnCastRejected;

    public override void _ExitTree() => Player.CastRejected -= OnCastRejected;

    public override void _Process(double delta)
    {
        for (var i = 0; i < _rejectLeft.Length; i++)
            _rejectLeft[i] = Mathf.Max(0, _rejectLeft[i] - delta);
        if (_pickerOpen && !World.CanChangeSummoner(Player.Hero))
            _pickerOpen = false;
        QueueRedraw();
    }

    void OnCastRejected(int button) => _rejectLeft[button] = RejectFlashSeconds;

    /// <summary>
    /// 按钮的圆心和半径：Q W E R 一排，大招最大；召唤师技能在它们左边，回城和恢复排在召唤师技能的左上方。
    /// </summary>
    static (Vector2 Center, float Radius) Button(int id) => id switch
    {
        0 => (new Vector2(130, 96), 34f),
        1 => (new Vector2(220, 96), 34f),
        2 => (new Vector2(310, 96), 34f),
        3 => (new Vector2(412, 90), 40f),
        Unit.SummonerSlot => (new Vector2(40, 108), 28f),
        RecallButton => (new Vector2(24, 48), 19f),
        _ => (new Vector2(70, 30), 19f),
    };

    static Vector2 LearnBadge(int slot)
    {
        var (center, radius) = Button(slot);
        return center + new Vector2(radius * 0.78f, -radius * 0.78f);
    }

    /// <summary>召唤师技能选单排在 Q W E 上方。</summary>
    static Vector2 PickerSlot(int index) => new(240 + (index - (Summoners.All.Count - 1) / 2f) * 46, 26);

    /// <summary>只有按钮、「+」和召唤师技能选单接收鼠标，控件里的空白处不挡住对地图的点击。</summary>
    public override bool _HasPoint(Vector2 point) => HitTest(point) is not null;

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
            return;
        AcceptEvent();
        var hero = Player.Hero;
        switch (HitTest(click.Position))
        {
            case ("pick", var index):
                World.SetSummoner(hero, Summoners.All[index]);
                _pickerOpen = false;
                break;
            case ("learn", var slot):
                World.LearnSkill(hero, slot);
                break;
            case ("button", RecallButton):
                Player.Recall();
                break;
            case ("button", RestoreButton):
                Player.Restore();
                break;
            case ("button", Unit.SummonerSlot) when World.CanChangeSummoner(hero):
                _pickerOpen = !_pickerOpen;
                break;
            case ("button", var slot):
                Player.BeginAim(slot, releaseToCast: false);
                break;
        }
    }

    (string Kind, int Index)? HitTest(Vector2 point)
    {
        var hero = Player.Hero;
        if (_pickerOpen)
        {
            for (var i = 0; i < Summoners.All.Count; i++)
            {
                if (point.DistanceTo(PickerSlot(i)) <= PickerRadius)
                    return ("pick", i);
            }
        }
        for (var i = 0; i < hero.Def.Skills.Count; i++)
        {
            if (World.CanLearn(hero, i) && point.DistanceTo(LearnBadge(i)) <= BadgeRadius + 2)
                return ("learn", i);
        }
        for (var id = 0; id < Keys.Length; id++)
        {
            var (center, radius) = Button(id);
            if (point.DistanceTo(center) <= radius + RingWidth * 2)
                return ("button", id);
        }
        return null;
    }

    public override void _Draw()
    {
        var hero = Player.Hero;
        for (var slot = 0; slot < hero.SkillCount; slot++)
            DrawSkill(hero, slot);
        DrawRecall(hero);
        DrawRestore(hero);
        if (_pickerOpen)
            DrawPicker(hero);
    }

    void DrawSkill(Unit hero, int slot)
    {
        var skill = hero.SkillAt(slot);
        var level = hero.SkillLevels[slot];
        var (center, radius) = Button(slot);
        var cooldown = hero.SkillCooldowns[slot];
        var total = SimTime.Ticks(skill.Cooldown.At(level));
        DrawFace(slot, center, radius, skill.Icon, level > 0, Player.AimingSlot == slot,
            cooldown > 0 ? cooldown / (float)total : 0f, cooldown);

        if (slot == Unit.SummonerSlot)
        {
            // 在泉水里可以更换：外圈加一道己方色细环
            if (World.CanChangeSummoner(hero))
                DrawArc(center, radius + 6, 0, Mathf.Tau, 48, Palette.Ally, 2f, true);
            DrawKey(slot);
            return;
        }

        // 等级圆环：每段代表一级，已学的段用己方色
        var segment = (Mathf.Tau - skill.MaxLevel * RingGap) / skill.MaxLevel;
        for (var l = 0; l < skill.MaxLevel; l++)
        {
            var start = -Mathf.Pi / 2 + RingGap / 2 + l * (segment + RingGap);
            DrawArc(center, radius + 8, start, start + segment, 16, l < level ? Palette.Ally : Border, RingWidth, true);
        }

        var font = ThemeDB.FallbackFont;
        if (level > 0)
        {
            var cost = skill.ManaCost.At(level);
            DrawString(font, center + new Vector2(-radius, radius + 26), $"{cost:0}", HorizontalAlignment.Center, radius * 2, 11,
                hero.Mana >= cost ? Muted : Palette.Enemy);
        }
        if (World.CanLearn(hero, slot))
        {
            var badge = LearnBadge(slot);
            DrawCircle(badge, BadgeRadius, Palette.Ally);
            DrawString(font, badge + new Vector2(-BadgeRadius, 6), "+", HorizontalAlignment.Center, BadgeRadius * 2, 18, Colors.White);
        }
        DrawKey(slot);
    }

    void DrawRecall(Unit hero)
    {
        var (center, radius) = Button(RecallButton);
        DrawFace(RecallButton, center, radius, "recall", hero.IsAlive, false, 0f, 0);
        if (hero.IsRecalling)
            DrawProgress(center, radius, 1f - hero.RecallTicks / (float)SimTime.Ticks(GameRules.RecallSeconds));
        DrawKey(RecallButton);
    }

    void DrawRestore(Unit hero)
    {
        var (center, radius) = Button(RestoreButton);
        var cooldown = hero.RestoreCooldown;
        DrawFace(RestoreButton, center, radius, "restore", hero.IsAlive, false,
            cooldown / (float)SimTime.Ticks(GameRules.RestoreCooldownSeconds), cooldown);
        if (hero.IsRestoring)
            DrawProgress(center, radius, hero.RestoreTicks / (float)SimTime.Ticks(GameRules.RestoreSeconds));
        DrawKey(RestoreButton);
    }

    void DrawPicker(Unit hero)
    {
        for (var i = 0; i < Summoners.All.Count; i++)
        {
            var at = PickerSlot(i);
            var chosen = hero.Summoner == Summoners.All[i];
            DrawCircle(at, PickerRadius, Colors.White);
            DrawArc(at, PickerRadius, 0, Mathf.Tau, 48, chosen ? Palette.Ally : Border, chosen ? 3f : 1.5f, true);
            SkillIcons.Draw(this, Summoners.All[i].Icon, at, PickerRadius * 0.5f, Palette.Neutral);
        }
    }

    /// <summary>按钮的底、外框、图标和冷却遮罩。</summary>
    void DrawFace(int id, Vector2 center, float radius, string icon, bool usable, bool aiming, float cooldownFraction, int cooldownTicks)
    {
        var font = ThemeDB.FallbackFont;
        var rejected = _rejectLeft[id] > 0;
        DrawCircle(center, radius, Colors.White);
        var outline = rejected ? Palette.Enemy : aiming ? Palette.Ally : Border;
        DrawArc(center, radius, 0, Mathf.Tau, 64, outline, rejected || aiming ? 3f : 1.5f, true);
        SkillIcons.Draw(this, icon, center, radius * 0.5f, usable ? Palette.Neutral : Muted);

        if (cooldownTicks > 0)
        {
            DrawColoredPolygon(Sector(center, radius, cooldownFraction), CooldownShade);
            DrawString(font, center + new Vector2(-radius, radius * 0.25f), $"{Mathf.CeilToInt(SimTime.Seconds(cooldownTicks))}",
                HorizontalAlignment.Center, radius * 2, Mathf.RoundToInt(radius * 0.65f), Colors.White);
        }
    }

    /// <summary>按钮左下角的按键标识，最后画，压在圆环之上。</summary>
    void DrawKey(int id)
    {
        var (center, radius) = Button(id);
        var size = Mathf.Max(16f, Mathf.Round(radius * 0.55f));
        var box = new Rect2(center + new Vector2(-radius * 0.85f - size / 2, radius * 0.85f - size / 2), size, size);
        DrawRect(box, Palette.Neutral);
        DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(0, size * 0.75f), Keys[id], HorizontalAlignment.Center, size,
            Mathf.RoundToInt(size * 0.65f), Colors.White);
    }

    void DrawProgress(Vector2 center, float radius, float fraction) =>
        DrawArc(center, radius + 5, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * fraction, 48, Palette.Ally, 3f, true);

    /// <summary>从正上方顺时针展开、占整圆 fraction 的扇形。</summary>
    static Vector2[] Sector(Vector2 center, float radius, float fraction)
    {
        const int steps = 32;
        var count = Mathf.Max(2, Mathf.CeilToInt(steps * fraction) + 1);
        var points = new Vector2[count + 1];
        points[0] = center;
        for (var i = 0; i < count; i++)
        {
            var angle = -Mathf.Pi / 2 + Mathf.Tau * fraction * i / (count - 1);
            points[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }
}

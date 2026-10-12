namespace HeightFall.Sim;

public static class Heroes
{
    /// <summary>「垒」：近战，塑造地形。</summary>
    public static readonly UnitDef Bastion = new()
    {
        Name = "垒", Model = "bastion", Kind = UnitKind.Hero, BodyHeight = 6.5f,
        MaxHp = 680, HpPerLevel = 90, HpRegen = 2f,
        MaxMana = 300, ManaPerLevel = 25, ManaRegen = 5f, ManaRegenPerLevel = 0.2f,
        PhysicalAttack = 55, PhysicalAttackPerLevel = 3.2f, MagicAttack = 50, MagicAttackPerLevel = 3f,
        AttackRange = 1.5f, AttackInterval = 1.3f,
        PhysicalDefense = 3, PhysicalDefensePerLevel = 0.35f, MagicDefense = 5.5f, MagicDefensePerLevel = 0.3f,
        MoveSpeed = 3f,
        Skills =
        [
            new SkillDef
            {
                Name = "裂地", Icon = "crack", Targeting = SkillTargeting.Direction,
                Area = new LineSelector(6, StopAtWall: true, HalfWidth: 1),
                Effects = [new DamageEffect([160, 230, 290, 340], DamageType.Magic)],
                Cooldown = [8, 7.5f, 7, 6.5f], ManaCost = [60, 65, 70, 75],
            },
            new SkillDef
            {
                Name = "隆起", Icon = "raise", Targeting = SkillTargeting.Tile, Range = 8,
                Area = new CircleSelector(1.5f),
                Effects = [new ShiftHeightEffect(+2, 4)],
                Cooldown = [14, 13, 12, 11], ManaCost = [50],
            },
            new SkillDef
            {
                Name = "塌陷", Icon = "lower", Targeting = SkillTargeting.Tile, Range = 7,
                Area = new CircleSelector(1),
                Effects = [new ShiftHeightEffect(-2, 3), new DamageEffect([120, 180, 230, 260], DamageType.Magic)],
                Cooldown = [12, 11, 10, 9], ManaCost = [55],
            },
            new SkillDef
            {
                Name = "断崖", Icon = "cliff", Targeting = SkillTargeting.Direction, IsUltimate = true,
                Area = new LineSelector(9, StopAtWall: false),
                Effects =
                [
                    new ShiftHeightEffect(+2, 5),
                    new DamageEffect([230, 300, 350], DamageType.Magic),
                    new StunEffect([1f, 1.25f, 1.5f]),
                ],
                Cooldown = [80, 70, 60], ManaCost = [120, 160, 200], CastPoint = 0.4f,
            },
        ],
    };

    /// <summary>「隼」：远程，地面与空中两种位移。</summary>
    public static readonly UnitDef Falcon = new()
    {
        Name = "隼", Model = "falcon", Kind = UnitKind.Hero, BodyHeight = 6.5f,
        MaxHp = 560, HpPerLevel = 75, HpRegen = 1.6f,
        MaxMana = 320, ManaPerLevel = 30, ManaRegen = 5.5f, ManaRegenPerLevel = 0.2f,
        PhysicalAttack = 50, PhysicalAttackPerLevel = 3.5f, MagicAttack = 45, MagicAttackPerLevel = 3f,
        AttackRange = 6f, AttackInterval = 1.1f, ProjectileSpeed = 11f, LaunchHeight = 3.5f,
        PhysicalDefense = 1, PhysicalDefensePerLevel = 0.25f, MagicDefense = 5.5f, MagicDefensePerLevel = 0.3f,
        MoveSpeed = 3.2f,
        Skills =
        [
            new SkillDef
            {
                Name = "穿云", Icon = "arrow", Targeting = SkillTargeting.Direction,
                Area = new LineSelector(9, StopAtWall: true),
                Effects = [new DamageEffect([180, 250, 300, 340], DamageType.Physical)],
                Cooldown = [7], ManaCost = [55, 60, 65, 70],
            },
            new SkillDef
            {
                Name = "掠影", Icon = "blink", Targeting = SkillTargeting.Tile, Range = 8,
                MaxRise = HeightRules.MaxStep,
                Area = new TargetTileSelector(),
                Effects = [new BlinkEffect()],
                Cooldown = [12, 10.5f, 9, 7.5f], ManaCost = [45], CastPoint = 0.1f,
            },
            new SkillDef
            {
                Name = "腾跃", Icon = "leap", Targeting = SkillTargeting.Tile, Range = 6,
                // 能跳上高台和隆起的地面；岩壁和边界比地面高 8 级，跳不上去
                MaxRise = 3,
                Area = new CircleSelector(1.5f),
                Effects =
                [
                    new LeapEffect(0.6f),
                    new DelayEffect(0.6f),
                    new DamageEffect([140, 190, 230, 270], DamageType.Physical),
                    new StunEffect([0.4f]),
                ],
                Cooldown = [14, 12, 10, 8], ManaCost = [60], CastPoint = 0.1f,
            },
            new SkillDef
            {
                Name = "天坠", Icon = "pit", Targeting = SkillTargeting.Tile, Range = 10, IsUltimate = true,
                Area = new CircleSelector(2.5f),
                Effects =
                [
                    new DelayEffect(1f),
                    new ShiftHeightEffect(-3, 4),
                    new DamageEffect([300, 340, 370], DamageType.Physical),
                ],
                Cooldown = [90, 75, 60], ManaCost = [150, 190, 230],
            },
        ],
    };
}

using HeightFall.Sim;

namespace HeightFall.Sim.Tests;

public class KillCreditTests
{
    static (World World, Unit A, Unit B, Unit Victim) Setup()
    {
        var world = new World(StandardMap.Build());
        var a = world.SpawnHero(Heroes.Bastion, Team.Heights);
        var b = world.SpawnHero(Heroes.Falcon, Team.Heights);
        var victim = world.SpawnHero(Heroes.Falcon, Team.Fall);
        var top = new GridPos(30, 20);
        world.Blink(victim, top);
        world.Blink(a, top + new GridPos(0, 3));
        world.Blink(b, top + new GridPos(0, 4));
        return (world, a, b, victim);
    }

    /// <summary>最外侧的一座塔（T1），不受攻击顺序保护。</summary>
    static Unit Tower(World world, Team team) => world.Units.First(u => u.Structure is { Kind: StructureKind.Tower, Tier: 1 } && u.Team == team);

    [Fact]
    public void LastHitByCreditedHeroEarnsFullGold()
    {
        var (world, a, _, victim) = Setup();
        var gold = a.Gold;

        world.DealDamage(a, victim, victim.MaxHp * 10, DamageType.Physical);

        Assert.True(victim.IsDead);
        Assert.Equal(1, a.Kills);
        Assert.Equal(1, world.TeamKills(Team.Heights));
        Assert.Equal(gold + GameRules.HeroKillGold(victim.Level), a.Gold);
    }

    [Fact]
    public void KillGoesToLastEnemyHeroWhoDealtDamageWithReducedGold()
    {
        var (world, a, b, victim) = Setup();
        var gold = a.Gold;

        world.DealDamage(a, victim, 10, DamageType.Physical);
        world.DealDamage(b, victim, 10, DamageType.Physical);
        world.DealDamage(Tower(world, Team.Heights), victim, victim.MaxHp * 10, DamageType.Physical);

        Assert.Equal(0, a.Kills);
        Assert.Equal(1, b.Kills);
        Assert.Equal(1, world.TeamKills(Team.Heights));
        Assert.Equal(gold, a.Gold);
        Assert.Equal(gold + (int)(GameRules.HeroKillGold(victim.Level) * GameRules.CreditedKillGoldFactor), b.Gold);
    }

    [Fact]
    public void LethalFallIsCreditedToLastEnemyHeroDamager()
    {
        var (world, a, _, victim) = Setup();
        var gold = a.Gold;
        world.DealDamage(a, victim, 10, DamageType.Magic);
        victim.Hp = 1;

        Assert.True(world.Map.AddHeightModifier(victim.Pos, -2, SimTime.Ticks(3)));

        Assert.True(victim.IsDead);
        Assert.Equal(1, a.Kills);
        Assert.Equal(gold + (int)(GameRules.HeroKillGold(victim.Level) * GameRules.CreditedKillGoldFactor), a.Gold);
    }

    [Fact]
    public void DenyStillWinsOverEnemyHeroDamage()
    {
        var (world, a, _, victim) = Setup();
        var ally = world.SpawnHero(Heroes.Bastion, Team.Fall);
        var gold = a.Gold;

        world.DealDamage(a, victim, 10, DamageType.Physical);
        world.DealDamage(ally, victim, victim.MaxHp * 10, DamageType.Physical);

        Assert.True(victim.IsDead);
        Assert.Equal(0, a.Kills);
        Assert.Equal(0, world.TeamKills(Team.Heights));
        Assert.Equal(gold, a.Gold);
        Assert.Equal(0, a.Xp);
    }

    [Fact]
    public void RespawnStartsANewLifeWithoutEarlierDamagers()
    {
        var (world, a, _, victim) = Setup();
        world.DealDamage(a, victim, 10, DamageType.Physical);
        world.DealDamage(Tower(world, Team.Heights), victim, victim.MaxHp * 10, DamageType.Physical);
        Assert.Equal(1, a.Kills);

        var respawnTicks = victim.RespawnTicks;
        for (var i = 0; i <= respawnTicks; i++)
            world.Step();
        Assert.True(victim.IsAlive);
        world.DealDamage(Tower(world, Team.Heights), victim, victim.MaxHp * 10, DamageType.Physical);

        Assert.Equal(1, a.Kills);
        Assert.Equal(2, world.TeamKills(Team.Heights));
    }

    [Fact]
    public void NeutralUnitIsCreditedToTheLastHeroOfEitherTeam()
    {
        var (world, a, _, fallHero) = Setup();
        var gold = a.Gold;
        // 地图上还没有生成中立单位，这里直接构造一个来验证归属规则。
        var neutral = new Unit(-1, UnitDefs.MeleeCreep, Team.Neutral, new GridPos(40, 40));
        neutral.Hp = neutral.MaxHp;

        world.DealDamage(a, neutral, 10, DamageType.Physical);
        world.DealDamage(fallHero, neutral, 10, DamageType.Physical);
        world.DealDamage(Tower(world, Team.Heights), neutral, neutral.MaxHp * 10, DamageType.Physical);

        Assert.True(neutral.IsRemoved);
        Assert.Equal(0, a.LastHits);
        Assert.Equal(1, fallHero.LastHits);
        Assert.Equal(gold, a.Gold);
        Assert.Equal(gold + (int)(UnitDefs.MeleeCreep.Gold * GameRules.CreditedKillGoldFactor), fallHero.Gold);
    }

    [Fact]
    public void StructuresStillPayTheWholeTeamInFull()
    {
        var (world, a, b, _) = Setup();
        var enemyTower = Tower(world, Team.Fall);
        var gold = a.Gold;

        world.DealDamage(a, enemyTower, 10, DamageType.Physical);
        world.DealDamage(Tower(world, Team.Heights), enemyTower, enemyTower.MaxHp * 10, DamageType.Physical);

        Assert.True(enemyTower.IsRemoved);
        Assert.Equal(gold + GameRules.StructureGold, a.Gold);
        Assert.Equal(gold + GameRules.StructureGold, b.Gold);
    }
}

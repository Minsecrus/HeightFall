namespace HeightFall.Sim;

public enum Team
{
    /// <summary>「岭」，基地在西南角。</summary>
    Heights,

    /// <summary>「渊」，基地在东北角。</summary>
    Fall,

    Neutral,
}

public static class TeamExtensions
{
    public static Team Opponent(this Team team) => team switch
    {
        Team.Heights => Team.Fall,
        Team.Fall => Team.Heights,
        _ => Team.Neutral,
    };
}

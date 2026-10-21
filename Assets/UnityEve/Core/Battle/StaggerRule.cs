namespace Unity.Eve.Core.Battle
{
    /// <summary>Enough burst damage in a short window staggers a creature: it loses its turn and
    /// reconsiders, same as a stun. See <see cref="BattleConfig.StaggerDamageFloor"/>/
    /// <see cref="BattleConfig.StaggerDamageFraction"/> for the threshold.</summary>
    public static class StaggerRule
    {
        public static bool ShouldStagger(float recentDamage, float maxHp) =>
            recentDamage >= System.Math.Max(BattleConfig.StaggerDamageFloor, maxHp * BattleConfig.StaggerDamageFraction);
    }
}

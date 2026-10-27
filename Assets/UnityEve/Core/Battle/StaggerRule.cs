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

    /// <summary>
    /// A creature's recent damage, as ExperimentEve's enemyActor.ts keeps it: every hit adds to it, it drains
    /// at <see cref="BattleConfig.StaggerDecayPerSecond"/> of game time, a creature already stunned is never
    /// staggered again, and it clears when a stun wears off. The drain is what makes the window "short":
    /// chip damage spread over a fight never adds up to a stagger.
    /// </summary>
    public sealed class StaggerMeter
    {
        private bool _wasStunned;

        public float Recent { get; private set; }

        /// <returns>True when this hit staggers the creature.</returns>
        public bool AddHit(float dealt, float maxHp, bool stunned)
        {
            if (dealt > 0f) Recent += dealt;
            return !stunned && StaggerRule.ShouldStagger(Recent, maxHp);
        }

        /// <param name="deltaSeconds">Scaled time: nothing drains while the world is paused.</param>
        /// <param name="stunned">Whether the creature is stunned now; the tick a stun ends clears the meter.</param>
        public void Tick(float deltaSeconds, bool stunned)
        {
            if (deltaSeconds <= 0f) return;
            Recent = System.Math.Max(0f, Recent - deltaSeconds * BattleConfig.StaggerDecayPerSecond);
            if (_wasStunned && !stunned) Recent = 0f;
            _wasStunned = stunned;
        }
    }
}

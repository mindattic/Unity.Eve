namespace UnityEve.Core.Battle
{
    /// <summary>
    /// Every battle tuning number in one place. Values follow Parasite Eve (1998) where the
    /// original is documented, and ExperimentEve's playtested numbers where it is not.
    /// </summary>
    public static class BattleConfig
    {
        // --- Active Time Bar ---------------------------------------------------------------
        // PE1: fill rate = (50 + ActiveTime - penalty*2) * gunMultiplier. A base rate of 50
        // fills the bar in BaseAtbSeconds; higher Active Time fills proportionally faster.
        public const float AtbBaseRate = 50f;
        public const float BaseAtbSeconds = 3.2f;
        public const float AtbMinimumRate = 19f;
        public const float HasteMultiplier = 2f;

        // --- Parasite Energy -------------------------------------------------------------------
        public const float PeRegenPerSecond = 4f;
        /// <summary>PE1: every cast in a fight slows PE recovery; after ~7 casts it barely moves.</summary>
        public const float PeRegenFalloffPerCast = 0.14f;
        public const float PeRegenFloor = 0.05f;

        // --- Damage ------------------------------------------------------------------------
        public const float DamageVarianceLow = 0.9f;
        public const float DamageVarianceHigh = 1.1f;
        public const float CriticalChance = 0.08f;
        public const float CriticalMultiplier = 1.75f;
        public const float DefensePerPoint = 0.5f;

        // --- Enemy pressure ----------------------------------------------------------------
        /// <summary>At most this many enemies may be winding up or striking at once.</summary>
        public const int MaxCommittedAttackers = 2;
        public const float StrikeSeconds = 0.14f;
        public const float StrikeWhiffSlack = 0.35f;

        // --- Player defence ----------------------------------------------------------------
        public const float DodgeSeconds = 0.34f;
        public const float DodgeInvulnerableSeconds = 0.5f;
        public const float DodgeCooldownSeconds = 0.9f;

        // --- Escape ------------------------------------------------------------------------
        public const float EscapeBaseChance = 0.6f;
        public const float EscapeChancePerLevel = 0.08f;
        public const float EscapeMinChance = 0.15f;
        public const float EscapeMaxChance = 0.95f;

        // --- Experience --------------------------------------------------------------------
        public const int ExpPerLevelBase = 60;
        public const float ExpCurveGrowth = 1.45f;
        public const int BonusPointsPerLevel = 3;
    }
}

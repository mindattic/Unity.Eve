namespace Unity.Eve.Core.Battle
{
    /// <summary>
    /// Every battle tuning number in one place. Values follow Parasite Eve (1998) where the
    /// original is documented, and ExperimentEve's playtested numbers where it is not. The shipped values
    /// are data: Resources/UnityEve/battle-tuning.json, applied through <see cref="Apply"/> before the first
    /// scene loads. The initializers here are the same numbers, so Core runs (and tests) without the file.
    /// </summary>
    public static partial class BattleConfig
    {
        // --- Active Time Bar ---------------------------------------------------------------
        // PE1: fill rate = (50 + ActiveTime - penalty*2) * gunMultiplier. A base rate of 50
        // fills the bar in BaseAtbSeconds; higher Active Time fills proportionally faster.
        public static float AtbBaseRate { get; private set; } = 50f;
        public static float BaseAtbSeconds { get; private set; } = 3.2f;
        public static float AtbMinimumRate { get; private set; } = 19f;
        public static float HasteMultiplier { get; private set; } = 2f;

        // --- Parasite Energy -------------------------------------------------------------------
        public static float PeRegenPerSecond { get; private set; } = 4f;
        /// <summary>PE1: every cast in a fight slows PE recovery; after ~7 casts it barely moves.</summary>
        public static float PeRegenFalloffPerCast { get; private set; } = 0.14f;
        public static float PeRegenFloor { get; private set; } = 0.05f;

        // --- Damage ------------------------------------------------------------------------
        public static float DamageVarianceLow { get; private set; } = 0.9f;
        public static float DamageVarianceHigh { get; private set; } = 1.1f;
        public static float CriticalChance { get; private set; } = 0.08f;
        public static float CriticalMultiplier { get; private set; } = 1.75f;
        public static float DefensePerPoint { get; private set; } = 0.5f;

        // --- Enemy pressure ----------------------------------------------------------------
        /// <summary>At most this many enemies may be winding up or striking at once.</summary>
        public static int MaxCommittedAttackers { get; private set; } = 2;
        public static float StrikeSeconds { get; private set; } = 0.14f;
        public static float StrikeWhiffSlack { get; private set; } = 0.35f;

        // --- Enemy tactics (EnemyMind) ------------------------------------------------------
        public static float EnemyThinkMinSeconds { get; private set; } = 0.4f;
        public static float EnemyThinkMaxSeconds { get; private set; } = 1.0f;
        /// <summary>Waiting creatures keep to a ring this far from the player (each picks its own radius).</summary>
        public static float EnemyRingMinRadius { get; private set; } = 3.6f;
        public static float EnemyRingMaxRadius { get; private set; } = 5.4f;
        /// <summary>Seconds a charging creature rears up (growls, holds still) before it runs.</summary>
        public static float EnemyChargeAnticipationSeconds { get; private set; } = 0.4f;
        public static float EnemyChargeMaxSeconds { get; private set; } = 2.2f;
        public static float EnemyChargeSpeed { get; private set; } = 2.4f;
        public static float EnemyRetreatSpeed { get; private set; } = 1.3f;
        /// <summary>Breather after an attack token comes back before the next creature may take it.</summary>
        public static float TokenBreatherMinSeconds { get; private set; } = 0.6f;
        public static float TokenBreatherMaxSeconds { get; private set; } = 1.5f;
        /// <summary>A flanker strikes once the player's facing to it drops below this cosine (~78 degrees off her aim).</summary>
        public static float EnemyFlankOpeningFacing { get; private set; } = 0.2f;
        public static float EnemyBuffCastSeconds { get; private set; } = 1.1f;
        public static float EnemyBuffSeconds { get; private set; } = 8f;
        public static float EnemyBuffCooldownSeconds { get; private set; } = 14f;
        public static float EnemyBuffDamageMultiplier { get; private set; } = 1.35f;
        public static float EnemyBuffTempo { get; private set; } = 1.2f;

        // --- Random encounters on returning to a screen ------------------------------------
        public static float ScreenEncounterChance { get; private set; } = 0.55f;
        public static float ScreenEncounterCooldownSeconds { get; private set; } = 12f;

        // --- Katana (BladeArts) ------------------------------------------------------------
        public static int KatanaDamage { get; private set; } = 24;
        /// <summary>Slash reach in metres (the slash range dome).</summary>
        public static float KatanaSlashReach { get; private set; } = 2.4f;
        public static float KatanaSlashArcDegrees { get; private set; } = 110f;
        /// <summary>Rush range in metres (the rush range dome).</summary>
        public static float KatanaRushRange { get; private set; } = 8f;
        public static float KatanaRushPeCost { get; private set; } = 12f;
        public static float KatanaRushHalfWidth { get; private set; } = 0.7f;
        public static float KatanaRushOvershoot { get; private set; } = 1.4f;
        public static float KatanaRushDamageMultiplier { get; private set; } = 0.85f;
        public static float KatanaRushSeconds { get; private set; } = 0.26f;
        /// <summary>How fast the stick rotates Line Rush's aim line, degrees per second at full deflection.</summary>
        public static float RushAimTurnDegreesPerSecond { get; private set; } = 150f;

        // --- Player defence ----------------------------------------------------------------
        public static float DodgeSeconds { get; private set; } = 0.34f;
        public static float DodgeInvulnerableSeconds { get; private set; } = 0.5f;
        public static float DodgeCooldownSeconds { get; private set; } = 0.9f;
        /// <summary>Kat's run speed, m/s. Every creature's default <c>MoveSpeed</c> must stay below this â€”
        /// she can always walk away from one that isn't actively charging, or the fight is unwinnable
        /// by distance alone. A short Charge (<see cref="EnemyChargeSpeed"/> multiplies it) may exceed it.</summary>
        public static float PlayerRunSpeed { get; private set; } = 4f;
        /// <summary>Stepping away from a target within this long of her last attack backpedals (faces the
        /// target, walks backward); past it, stepping away turns and runs like ordinary retreat.</summary>
        public static float BackpedalWindowSeconds { get; private set; } = 3f;

        // --- Shove (free melee: Interact in battle, no ATB cost, no ammo) --------------------
        public static float ShoveReach { get; private set; } = 1.6f;
        public static float ShoveCooldownSeconds { get; private set; } = 2.5f;
        public static float ShoveRootSeconds { get; private set; } = 0.3f;
        public static float ShoveKnockbackMetres { get; private set; } = 1.8f;
        public static float ShoveStunSeconds { get; private set; } = 1.2f;
        /// <summary>Fraction of her current weapon's damage a shove lands: control, not a damage option.</summary>
        public static float ShoveDamageFraction { get; private set; } = 0.25f;

        // --- Shootdodge (Max Payne-style dive: leap, fire in bullet time, land hard) ----------
        /// <summary>Horizontal dive speed, metres/second of real (unscaled) time â€” the dive itself isn't
        /// slowed by its own bullet time, only the world around it is, same as Max's own dive.</summary>
        public static float ShootdodgeSpeed { get; private set; } = 6.5f;
        public static float ShootdodgeRiseSpeed { get; private set; } = 4.5f;
        public static float ShootdodgeGravity { get; private set; } = 11f;
        /// <summary>The world (enemy attacks, their windups) runs at this fraction of normal speed while
        /// she's airborne â€” bullet time, via WorldTime's scale key, never Time.timeScale directly.</summary>
        public static float ShootdodgeTimeScale { get; private set; } = 0.3f;
        public static float ShootdodgeCooldownSeconds { get; private set; } = 6f;
        /// <summary>Rooted this long after landing â€” "you need time to stand up," the move's real cost.</summary>
        public static float ShootdodgeLandStunSeconds { get; private set; } = 1.4f;
        /// <summary>Flat self-damage from landing hard, regardless of defense: minor, but real.</summary>
        public static int ShootdodgeLandDamage { get; private set; } = 6;

        // --- Hazard puddles (a ranged hit that leaves acid/slime behind) ---------------------
        public static float PuddleDurationSeconds { get; private set; } = 6f;
        public static float PuddleRadius { get; private set; } = 1.3f;
        public static float PuddleTickSeconds { get; private set; } = 1f;
        public static int PuddleDamagePerTick { get; private set; } = 4;

        // --- Cover (benches, streetlamps, mailboxes) ----------------------------------------
        /// <summary>Damage reduction at full (1.0) exposure-blocked, scaled by how much of Kat's
        /// silhouette a cover prop actually blocks from the attacker (see Runtime/Battle/CoverProp.cs).</summary>
        public static float CoverMaxReduction { get; private set; } = 0.9f;

        // --- Active reload (Gears-of-War-style: press Reload again in the sweet spot) --------
        /// <summary>Full bar duration, seconds; a normal reload (no attempt, or a miss) takes this long.</summary>
        public static float ActiveReloadDurationSeconds { get; private set; } = 1.2f;
        /// <summary>Sweet-spot width as a fraction of the bar.</summary>
        public static float ActiveReloadSweetSpotWidth { get; private set; } = 0.12f;

        // --- Escape ------------------------------------------------------------------------
        public static float EscapeBaseChance { get; private set; } = 0.6f;
        public static float EscapeChancePerLevel { get; private set; } = 0.08f;
        public static float EscapeMinChance { get; private set; } = 0.15f;
        public static float EscapeMaxChance { get; private set; } = 0.95f;

        // --- Stagger (burst damage in a short window interrupts a creature's turn) ----------
        public static float StaggerDamageFloor { get; private set; } = 20f;
        public static float StaggerDamageFraction { get; private set; } = 0.35f;
        public static float StaggerStunSeconds { get; private set; } = 1.8f;
        /// <summary>Recent damage drains this much per second of game time (ExperimentEve enemyActor.ts tickStun).</summary>
        public static float StaggerDecayPerSecond { get; private set; } = 14f;

        // --- Experience --------------------------------------------------------------------
        public static int ExpPerLevelBase { get; private set; } = 60;
        public static float ExpCurveGrowth { get; private set; } = 1.45f;
        public static int BonusPointsPerLevel { get; private set; } = 3;
    }
}

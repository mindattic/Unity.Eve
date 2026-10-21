namespace Unity.Eve.Core.Battle
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

        // --- Enemy tactics (EnemyMind) ------------------------------------------------------
        public const float EnemyThinkMinSeconds = 0.4f;
        public const float EnemyThinkMaxSeconds = 1.0f;
        /// <summary>Waiting creatures keep to a ring this far from the player (each picks its own radius).</summary>
        public const float EnemyRingMinRadius = 3.6f;
        public const float EnemyRingMaxRadius = 5.4f;
        /// <summary>Seconds a charging creature rears up (growls, holds still) before it runs.</summary>
        public const float EnemyChargeAnticipationSeconds = 0.4f;
        public const float EnemyChargeMaxSeconds = 2.2f;
        public const float EnemyChargeSpeed = 2.4f;
        public const float EnemyRetreatSpeed = 1.3f;
        /// <summary>Breather after an attack token comes back before the next creature may take it.</summary>
        public const float TokenBreatherMinSeconds = 0.6f;
        public const float TokenBreatherMaxSeconds = 1.5f;
        /// <summary>A flanker strikes once the player's facing to it drops below this cosine (~78 degrees off her aim).</summary>
        public const float EnemyFlankOpeningFacing = 0.2f;
        public const float EnemyBuffCastSeconds = 1.1f;
        public const float EnemyBuffSeconds = 8f;
        public const float EnemyBuffCooldownSeconds = 14f;
        public const float EnemyBuffDamageMultiplier = 1.35f;
        public const float EnemyBuffTempo = 1.2f;

        // --- Random encounters on returning to a screen ------------------------------------
        public const float ScreenEncounterChance = 0.55f;
        public const float ScreenEncounterCooldownSeconds = 12f;

        // --- Katana (BladeArts) ------------------------------------------------------------
        public const int KatanaDamage = 24;
        /// <summary>Slash reach in metres (the slash range dome).</summary>
        public const float KatanaSlashReach = 2.4f;
        public const float KatanaSlashArcDegrees = 110f;
        /// <summary>Rush range in metres (the rush range dome).</summary>
        public const float KatanaRushRange = 8f;
        public const float KatanaRushPeCost = 12f;
        public const float KatanaRushHalfWidth = 0.7f;
        public const float KatanaRushOvershoot = 1.4f;
        public const float KatanaRushDamageMultiplier = 0.85f;
        public const float KatanaRushSeconds = 0.26f;
        /// <summary>How fast the stick rotates Line Rush's aim line, degrees per second at full deflection.</summary>
        public const float RushAimTurnDegreesPerSecond = 150f;

        // --- Player defence ----------------------------------------------------------------
        public const float DodgeSeconds = 0.34f;
        public const float DodgeInvulnerableSeconds = 0.5f;
        public const float DodgeCooldownSeconds = 0.9f;
        /// <summary>Kat's run speed, m/s. Every creature's default <c>MoveSpeed</c> must stay below this —
        /// she can always walk away from one that isn't actively charging, or the fight is unwinnable
        /// by distance alone. A short Charge (<see cref="EnemyChargeSpeed"/> multiplies it) may exceed it.</summary>
        public const float PlayerRunSpeed = 4f;
        /// <summary>Stepping away from a target within this long of her last attack backpedals (faces the
        /// target, walks backward); past it, stepping away turns and runs like ordinary retreat.</summary>
        public const float BackpedalWindowSeconds = 3f;

        // --- Shove (free melee: Interact in battle, no ATB cost, no ammo) --------------------
        public const float ShoveReach = 1.6f;
        public const float ShoveCooldownSeconds = 2.5f;
        public const float ShoveRootSeconds = 0.3f;
        public const float ShoveKnockbackMetres = 1.8f;
        public const float ShoveStunSeconds = 1.2f;
        /// <summary>Fraction of her current weapon's damage a shove lands: control, not a damage option.</summary>
        public const float ShoveDamageFraction = 0.25f;

        // --- Shootdodge (Max Payne-style dive: leap, fire in bullet time, land hard) ----------
        /// <summary>Horizontal dive speed, metres/second of real (unscaled) time — the dive itself isn't
        /// slowed by its own bullet time, only the world around it is, same as Max's own dive.</summary>
        public const float ShootdodgeSpeed = 6.5f;
        public const float ShootdodgeRiseSpeed = 4.5f;
        public const float ShootdodgeGravity = 11f;
        /// <summary>The world (enemy attacks, their windups) runs at this fraction of normal speed while
        /// she's airborne — bullet time, via WorldTime's scale key, never Time.timeScale directly.</summary>
        public const float ShootdodgeTimeScale = 0.3f;
        public const float ShootdodgeCooldownSeconds = 6f;
        /// <summary>Rooted this long after landing — "you need time to stand up," the move's real cost.</summary>
        public const float ShootdodgeLandStunSeconds = 1.4f;
        /// <summary>Flat self-damage from landing hard, regardless of defense: minor, but real.</summary>
        public const int ShootdodgeLandDamage = 6;

        // --- Hazard puddles (a ranged hit that leaves acid/slime behind) ---------------------
        public const float PuddleDurationSeconds = 6f;
        public const float PuddleRadius = 1.3f;
        public const float PuddleTickSeconds = 1f;
        public const int PuddleDamagePerTick = 4;

        // --- Cover (benches, streetlamps, mailboxes) ----------------------------------------
        /// <summary>Damage reduction at full (1.0) exposure-blocked, scaled by how much of Kat's
        /// silhouette a cover prop actually blocks from the attacker (see Runtime/Battle/CoverProp.cs).</summary>
        public const float CoverMaxReduction = 0.9f;

        // --- Active reload (Gears-of-War-style: press Reload again in the sweet spot) --------
        /// <summary>Full bar duration, seconds; a normal reload (no attempt, or a miss) takes this long.</summary>
        public const float ActiveReloadDurationSeconds = 1.2f;
        /// <summary>Sweet-spot width as a fraction of the bar.</summary>
        public const float ActiveReloadSweetSpotWidth = 0.12f;

        // --- Escape ------------------------------------------------------------------------
        public const float EscapeBaseChance = 0.6f;
        public const float EscapeChancePerLevel = 0.08f;
        public const float EscapeMinChance = 0.15f;
        public const float EscapeMaxChance = 0.95f;

        // --- Stagger (burst damage in a short window interrupts a creature's turn) ----------
        public const float StaggerDamageFloor = 20f;
        public const float StaggerDamageFraction = 0.35f;
        public const float StaggerStunSeconds = 1.8f;

        // --- Experience --------------------------------------------------------------------
        public const int ExpPerLevelBase = 60;
        public const float ExpCurveGrowth = 1.45f;
        public const int BonusPointsPerLevel = 3;
    }
}

using System;
using System.Collections.Generic;
using Unity.Eve.Core.Util;

namespace Unity.Eve.Core.Battle
{
    public readonly struct HitResult
    {
        public readonly int Amount;
        public readonly bool Critical;

        public HitResult(int amount, bool critical)
        {
            Amount = amount;
            Critical = critical;
        }
    }

    /// <summary>Damage rolls for weapons, abilities and items. Pure and seeded.</summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// PE1 rate-of-fire table: with N rounds per turn, each round deals this fraction of a single
        /// shot (2→60%, 3→41.7%, 5→25%, 7→18.2%, 10→13%), so more rounds trade per-hit power for total.
        /// </summary>
        public static float RoundShare(int roundsPerTurn)
        {
            switch (roundsPerTurn)
            {
                case <= 1: return 1f;
                case 2: return 0.6f;
                case 3: return 0.417f;
                case 4: return 0.31f;
                case 5: return 0.25f;
                case 6: return 0.21f;
                case 7: return 0.182f;
                default: return 1.3f / roundsPerTurn;
            }
        }

        public static HitResult Roll(int basePower, float multiplier, int defense, Rng rng, bool canCrit = true)
        {
            var variance = rng.Range(BattleConfig.DamageVarianceLow, BattleConfig.DamageVarianceHigh);
            var critical = canCrit && rng.Chance(BattleConfig.CriticalChance);
            var raw = basePower * multiplier * variance * (critical ? BattleConfig.CriticalMultiplier : 1f);
            var mitigated = raw - defense * BattleConfig.DefensePerPoint;
            return new HitResult(Math.Max(1, (int)Math.Round(mitigated)), critical);
        }

        /// <summary>
        /// Plans a weapon turn: which target each round hits. Single-target weapons put every round on
        /// the chosen target; Burst weapons deal rounds round-robin across every target in range.
        /// </summary>
        public static List<int> PlanRounds(WeaponStats weapon, int chosenTarget, int targetsInRange, int roundsAvailable)
        {
            var plan = new List<int>();
            var rounds = Math.Min(Math.Max(1, weapon.RoundsPerTurn), weapon.Melee ? weapon.RoundsPerTurn : roundsAvailable);
            if (targetsInRange <= 0 || rounds <= 0) return plan;
            if (!weapon.Burst)
            {
                for (var i = 0; i < rounds; i++) plan.Add(chosenTarget);
                return plan;
            }
            // Burst hits every target at least once if it has the rounds, starting from the chosen one.
            var total = Math.Max(rounds, Math.Min(targetsInRange, roundsAvailable));
            for (var i = 0; i < total; i++) plan.Add((chosenTarget + i) % targetsInRange);
            return plan;
        }
    }
}

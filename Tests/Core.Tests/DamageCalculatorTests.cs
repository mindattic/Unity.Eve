using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Util;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>Edge cases of the numbers behind every hit: the PE1 rate-of-fire table, the roll and round planning.</summary>
    public class DamageCalculatorTests
    {
        [TestCase(0, 1f)]
        [TestCase(1, 1f)]
        [TestCase(2, 0.6f)]
        [TestCase(3, 0.417f)]
        [TestCase(4, 0.31f)]
        [TestCase(5, 0.25f)]
        [TestCase(6, 0.21f)]
        [TestCase(7, 0.182f)]
        public void RoundShare_follows_the_pe1_table(int rounds, float share)
        {
            Assert.That(DamageCalculator.RoundShare(rounds), Is.EqualTo(share).Within(1e-6f));
        }

        [TestCase(8)]
        [TestCase(10)]
        [TestCase(20)]
        public void RoundShare_falls_back_to_one_point_three_over_n_from_eight_rounds(int rounds)
        {
            Assert.That(DamageCalculator.RoundShare(rounds), Is.EqualTo(1.3f / rounds).Within(1e-6f));
        }

        [Test]
        public void Variance_stays_within_its_band_without_crits()
        {
            var rng = new Rng(21);
            for (var i = 0; i < 5000; i++)
            {
                var hit = DamageCalculator.Roll(1000, 1f, 0, rng, canCrit: false);
                Assert.That(hit.Critical, Is.False);
                Assert.That(hit.Amount, Is.InRange(
                    (int)(1000 * BattleConfig.DamageVarianceLow),
                    (int)(1000 * BattleConfig.DamageVarianceHigh)));
            }
        }

        [Test]
        public void A_crit_applies_the_critical_multiplier()
        {
            var rng = new Rng(22);
            var crits = 0;
            for (var i = 0; i < 5000; i++)
            {
                var hit = DamageCalculator.Roll(1000, 1f, 0, rng);
                if (!hit.Critical) continue;
                crits++;
                Assert.That(hit.Amount, Is.InRange(
                    (int)(1000 * BattleConfig.DamageVarianceLow * BattleConfig.CriticalMultiplier),
                    (int)(1000 * BattleConfig.DamageVarianceHigh * BattleConfig.CriticalMultiplier) + 1));
            }
            Assert.That(crits, Is.GreaterThan(0), "CriticalChance should fire at least once in 5000 rolls");
        }

        [Test]
        public void Defense_subtracts_per_point()
        {
            // With no crit, variance is the only spread: the defended roll sits DefensePerPoint*def below the open one.
            var open = DamageCalculator.Roll(100, 1f, 0, new Rng(23), canCrit: false).Amount;
            var defended = DamageCalculator.Roll(100, 1f, 20, new Rng(23), canCrit: false).Amount;
            Assert.That(open - defended, Is.EqualTo((int)(20 * BattleConfig.DefensePerPoint)).Within(1));
        }

        [Test]
        public void Damage_floors_at_one_under_overwhelming_defense()
        {
            var rng = new Rng(24);
            for (var i = 0; i < 200; i++)
                Assert.That(DamageCalculator.Roll(5, 1f, 10000, rng).Amount, Is.EqualTo(1));
        }

        private static WeaponStats Gun(int rounds, bool burst = false, bool melee = false) =>
            new WeaponStats { RoundsPerTurn = rounds, Burst = burst, Melee = melee };

        [Test]
        public void Plan_is_empty_with_no_targets()
        {
            Assert.That(DamageCalculator.PlanRounds(Gun(2), 0, targetsInRange: 0, roundsAvailable: 10), Is.Empty);
        }

        [Test]
        public void Plan_is_empty_with_no_ammo()
        {
            Assert.That(DamageCalculator.PlanRounds(Gun(2), 0, targetsInRange: 3, roundsAvailable: 0), Is.Empty);
            Assert.That(DamageCalculator.PlanRounds(Gun(2, burst: true), 0, 3, 0), Is.Empty);
        }

        [Test]
        public void Melee_ignores_ammo()
        {
            Assert.That(DamageCalculator.PlanRounds(Gun(1, melee: true), 1, targetsInRange: 2, roundsAvailable: 0),
                Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Single_target_rounds_are_capped_by_ammo()
        {
            Assert.That(DamageCalculator.PlanRounds(Gun(3), 2, targetsInRange: 4, roundsAvailable: 2),
                Is.EqualTo(new[] { 2, 2 }));
        }

        [Test]
        public void Burst_with_more_targets_than_rounds_wraps_from_the_chosen_index()
        {
            Assert.That(DamageCalculator.PlanRounds(Gun(2, burst: true), 2, targetsInRange: 4, roundsAvailable: 10),
                Is.EqualTo(new[] { 2, 3, 0, 1 }));
        }

        [Test]
        public void Burst_hits_only_as_many_targets_as_it_has_rounds()
        {
            Assert.That(DamageCalculator.PlanRounds(Gun(1, burst: true), 3, targetsInRange: 5, roundsAvailable: 2),
                Is.EqualTo(new[] { 3, 4 }));
        }
    }
}

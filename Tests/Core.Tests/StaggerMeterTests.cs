using Unity.Eve.Core.Battle;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>Stagger is burst damage in a short window (U.EVE-T34): recent damage drains, as ExperimentEve's did.</summary>
    public class StaggerMeterTests
    {
        private const float BossHp = 400f; // threshold 140

        [Test]
        public void Chip_damage_spread_over_a_fight_never_staggers()
        {
            var meter = new StaggerMeter();
            for (var hit = 0; hit < 100; hit++)
            {
                Assert.That(meter.AddHit(12f, BossHp, stunned: false), Is.False, $"hit {hit}");
                meter.Tick(1f, stunned: false); // one hit a second drains faster than it fills
            }
        }

        [Test]
        public void A_burst_inside_the_window_staggers()
        {
            var meter = new StaggerMeter();
            Assert.That(meter.AddHit(80f, BossHp, false), Is.False);
            meter.Tick(0.5f, false);
            Assert.That(meter.AddHit(70f, BossHp, false), Is.True, "80 - 7 + 70 = 143 >= 140");
        }

        [Test]
        public void Damage_drains_at_the_decay_rate_and_never_below_zero()
        {
            var meter = new StaggerMeter();
            meter.AddHit(30f, BossHp, false);
            meter.Tick(1f, false);
            Assert.That(meter.Recent, Is.EqualTo(30f - BattleConfig.StaggerDecayPerSecond).Within(1e-4f));
            meter.Tick(10f, false);
            Assert.That(meter.Recent, Is.EqualTo(0f));
        }

        [Test]
        public void Nothing_drains_while_the_world_is_paused()
        {
            var meter = new StaggerMeter();
            meter.AddHit(30f, BossHp, false);
            meter.Tick(0f, false);
            Assert.That(meter.Recent, Is.EqualTo(30f));
        }

        [Test]
        public void A_stunned_creature_is_not_staggered_again_and_the_meter_clears_when_the_stun_ends()
        {
            var meter = new StaggerMeter();
            Assert.That(meter.AddHit(200f, BossHp, stunned: true), Is.False);
            meter.Tick(0.1f, stunned: true);
            Assert.That(meter.Recent, Is.GreaterThan(0f));
            meter.Tick(0.1f, stunned: false);
            Assert.That(meter.Recent, Is.EqualTo(0f));
        }
    }
}

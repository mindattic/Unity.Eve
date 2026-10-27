using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Util;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>The pack's turn-taking tokens in isolation, so a regression shows here rather than as vague mob behaviour.</summary>
    public class AttackTokensTests
    {
        private readonly object _rat = new object();
        private readonly object _spider = new object();
        private readonly object _snake = new object();

        [TestCase(0)]
        [TestCase(-3)]
        public void Capacity_below_one_clamps_to_one(int capacity)
        {
            Assert.That(new AttackTokens(capacity, new Rng(1)).Capacity, Is.EqualTo(1));
        }

        [TestCase(1, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(9, 2)]
        public void CapacityFor_grows_to_two_at_a_pack_of_four(int pack, int expected)
        {
            Assert.That(AttackTokens.CapacityFor(pack), Is.EqualTo(expected));
        }

        [Test]
        public void First_delay_blocks_takers_until_enough_scaled_time_passes()
        {
            var tokens = new AttackTokens(1, new Rng(2), firstDelay: 1f);
            Assert.That(tokens.TryTake(_rat), Is.False);
            tokens.Tick(0.6f);
            Assert.That(tokens.TryTake(_rat), Is.False);
            tokens.Tick(0.4f);
            Assert.That(tokens.TryTake(_rat), Is.True);
        }

        [Test]
        public void Zero_and_negative_ticks_are_ignored()
        {
            var tokens = new AttackTokens(1, new Rng(3), firstDelay: 0.5f);
            tokens.Tick(0f);
            tokens.Tick(-10f);
            Assert.That(tokens.Available, Is.False);
            tokens.Tick(0.5f);
            Assert.That(tokens.Available, Is.True);
        }

        [Test]
        public void TryTake_is_idempotent_for_an_existing_holder()
        {
            var tokens = new AttackTokens(1, new Rng(4));
            Assert.That(tokens.TryTake(_rat), Is.True);
            Assert.That(tokens.TryTake(_rat), Is.True);
            Assert.That(tokens.Held, Is.EqualTo(1));
            Assert.That(tokens.TryTake(_spider), Is.False);
        }

        [Test]
        public void Releasing_a_non_holder_starts_no_breather()
        {
            var tokens = new AttackTokens(2, new Rng(5));
            tokens.TryTake(_rat);
            tokens.Release(_spider);
            Assert.That(tokens.TryTake(_snake), Is.True);
        }

        [Test]
        public void A_real_release_blocks_the_next_taker_for_a_seeded_breather_in_range()
        {
            for (uint seed = 0; seed < 50; seed++)
            {
                var tokens = new AttackTokens(1, new Rng(seed));
                tokens.TryTake(_rat);
                tokens.Release(_rat);
                Assert.That(tokens.Held, Is.EqualTo(0));
                Assert.That(tokens.TryTake(_spider), Is.False, "no taker during the breather");

                var waited = 0f;
                const float step = 0.01f;
                while (!tokens.Available && waited < 5f)
                {
                    tokens.Tick(step);
                    waited += step;
                }
                Assert.That(waited, Is.GreaterThanOrEqualTo(BattleConfig.TokenBreatherMinSeconds - step)
                    .And.LessThanOrEqualTo(BattleConfig.TokenBreatherMaxSeconds + step), $"seed {seed}");
                Assert.That(tokens.TryTake(_spider), Is.True);
            }
        }

        [Test]
        public void The_breather_replays_from_its_seed()
        {
            float Breather(uint seed)
            {
                var tokens = new AttackTokens(1, new Rng(seed));
                tokens.TryTake(_rat);
                tokens.Release(_rat);
                var t = 0f;
                while (!tokens.Available) { tokens.Tick(0.001f); t += 0.001f; }
                return t;
            }
            Assert.That(Breather(77), Is.EqualTo(Breather(77)));
        }
    }
}

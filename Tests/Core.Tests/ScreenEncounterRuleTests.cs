using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Util;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    public class ScreenEncounterRuleTests
    {
        // A street of screens in a row: 1 - 2 - 3 - 4.
        private static ScreenEncounterRule Street(float chance = 1f)
        {
            var rule = new ScreenEncounterRule(new Rng(1), initialCooldownElapsed: 999f) { Chance = chance };
            rule.Connect(1, 2);
            rule.Connect(2, 3);
            rule.Connect(3, 4);
            return rule;
        }

        [Test]
        public void First_visits_never_trigger()
        {
            var rule = Street();
            Assert.That(rule.Enter(1), Is.False);
            Assert.That(rule.Enter(2), Is.False);
            Assert.That(rule.Enter(3), Is.False);
        }

        [Test]
        public void Bouncing_between_adjacent_screens_never_triggers()
        {
            var rule = Street();
            rule.Enter(1);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(rule.Enter(2), Is.False);
                Assert.That(rule.Enter(1), Is.False);
            }
        }

        [Test]
        public void Returning_after_going_somewhere_non_adjacent_can_trigger()
        {
            var rule = Street();
            rule.Enter(1);
            rule.Enter(2);
            rule.Enter(3);          // not adjacent to 1: screen 1 is now "far"
            rule.Enter(2);          // adjacent to 1, but 1 was already marked far
            Assert.That(rule.IsReturn(1), Is.True);
            Assert.That(rule.Enter(1), Is.True);
            Assert.That(rule.IsReturn(1), Is.False); // consumed: the next return needs another trip away
        }

        [Test]
        public void Screens_without_a_table_count_as_visits_but_never_host()
        {
            var rule = Street();
            rule.Enter(1);
            rule.Enter(99, canHost: false); // an interior, adjacent to nothing on the street
            Assert.That(rule.Enter(1), Is.True);
            rule.Enter(3);
            Assert.That(rule.Enter(99, canHost: false), Is.False);
        }

        [Test]
        public void Cooldown_after_a_battle_blocks_rolls_until_it_runs_out_on_scaled_time()
        {
            var rule = Street();
            rule.BattleEnded();
            rule.Enter(1);
            rule.Enter(3);
            Assert.That(rule.Enter(1), Is.False); // qualifying return, but still cooling down

            rule.Enter(3);
            rule.Tick(0f); // paused: no progress
            rule.Tick(BattleConfig.ScreenEncounterCooldownSeconds + 0.1f);
            Assert.That(rule.Enter(1), Is.True);
        }

        [Test]
        public void Chance_rolls_through_the_seeded_rng()
        {
            int Count(uint seed)
            {
                var rule = new ScreenEncounterRule(new Rng(seed), 999f) { Chance = 0.5f };
                rule.Connect(1, 2);
                var hits = 0;
                rule.Enter(1);
                for (var i = 0; i < 400; i++)
                {
                    rule.Enter(3); // far from 1
                    if (rule.Enter(1)) hits++;
                }
                return hits;
            }
            Assert.That(Count(5), Is.EqualTo(Count(5)));
            Assert.That(Count(5), Is.InRange(140, 260));
        }
    }
}

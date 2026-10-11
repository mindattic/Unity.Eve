using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using Xunit;

namespace UnityEve.Core.Tests
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

        [Fact]
        public void First_visits_never_trigger()
        {
            var rule = Street();
            Assert.False(rule.Enter(1));
            Assert.False(rule.Enter(2));
            Assert.False(rule.Enter(3));
        }

        [Fact]
        public void Bouncing_between_adjacent_screens_never_triggers()
        {
            var rule = Street();
            rule.Enter(1);
            for (var i = 0; i < 10; i++)
            {
                Assert.False(rule.Enter(2));
                Assert.False(rule.Enter(1));
            }
        }

        [Fact]
        public void Returning_after_going_somewhere_non_adjacent_can_trigger()
        {
            var rule = Street();
            rule.Enter(1);
            rule.Enter(2);
            rule.Enter(3);          // not adjacent to 1: screen 1 is now "far"
            rule.Enter(2);          // adjacent to 1, but 1 was already marked far
            Assert.True(rule.IsReturn(1));
            Assert.True(rule.Enter(1));
            Assert.False(rule.IsReturn(1)); // consumed: the next return needs another trip away
        }

        [Fact]
        public void Screens_without_a_table_count_as_visits_but_never_host()
        {
            var rule = Street();
            rule.Enter(1);
            rule.Enter(99, canHost: false); // an interior, adjacent to nothing on the street
            Assert.True(rule.Enter(1));
            rule.Enter(3);
            Assert.False(rule.Enter(99, canHost: false));
        }

        [Fact]
        public void Cooldown_after_a_battle_blocks_rolls_until_it_runs_out_on_scaled_time()
        {
            var rule = Street();
            rule.BattleEnded();
            rule.Enter(1);
            rule.Enter(3);
            Assert.False(rule.Enter(1)); // qualifying return, but still cooling down

            rule.Enter(3);
            rule.Tick(0f); // paused: no progress
            rule.Tick(BattleConfig.ScreenEncounterCooldownSeconds + 0.1f);
            Assert.True(rule.Enter(1));
        }

        [Fact]
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
            Assert.Equal(Count(5), Count(5));
            Assert.InRange(Count(5), 140, 260);
        }
    }
}

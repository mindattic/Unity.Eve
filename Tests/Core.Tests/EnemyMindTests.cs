using System.Collections.Generic;
using System.Linq;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using Xunit;

namespace UnityEve.Core.Tests
{
    public class EnemyMindTests
    {
        private static EnemySenses Senses(float distance = 3f, float health = 1f, float facing = 1f, bool slotFree = true, int pack = 2) =>
            new EnemySenses { Distance = distance, HealthFraction = health, PlayerFacing = facing, AttackSlotFree = slotFree, PackAlive = pack };

        [Fact]
        public void Mind_stops_to_think_first_and_freezes_while_the_world_is_paused()
        {
            var mind = new EnemyMind(new EnemyStats(), new Rng(1), firstThinkSeconds: 0.5f);
            Assert.Equal(EnemyIntent.Think, mind.Intent);
            Assert.False(mind.MayAttack(Senses(distance: 0.5f)));
            Assert.True(mind.Steer(Senses()).Still);

            mind.Tick(0f, Senses());
            Assert.Equal(EnemyIntent.Think, mind.Intent);

            mind.Tick(0.6f, Senses());
            Assert.NotEqual(EnemyIntent.Think, mind.Intent);
        }

        [Fact]
        public void Every_intent_is_followed_by_a_thinking_pause()
        {
            var mind = new EnemyMind(new EnemyStats(), new Rng(7));
            mind.Tick(0.01f, Senses());
            var chosen = mind.Intent;
            Assert.NotEqual(EnemyIntent.Think, chosen);
            mind.Tick(5f, Senses());
            Assert.Equal(EnemyIntent.Think, mind.Intent);
        }

        [Fact]
        public void Temperament_shapes_choices_aggressive_press_cautious_hang_back()
        {
            int Count(EnemyStats stats, EnemyIntent intent)
            {
                var mind = new EnemyMind(stats, new Rng(42));
                return Enumerable.Range(0, 2000).Count(_ => mind.Decide(Senses(facing: 0f)) == intent);
            }
            var brute = new EnemyStats { Aggression = 1f, Caution = 0f };
            var coward = new EnemyStats { Aggression = 0f, Caution = 1f };
            Assert.True(Count(brute, EnemyIntent.Approach) > 2 * Count(coward, EnemyIntent.Approach));
            Assert.True(Count(coward, EnemyIntent.Hold) > 2 * Count(brute, EnemyIntent.Hold));
            Assert.True(Count(coward, EnemyIntent.Buff) > Count(brute, EnemyIntent.Buff));
        }

        [Fact]
        public void Hurt_cautious_creatures_retreat_and_healthy_ones_do_not()
        {
            var mind = new EnemyMind(new EnemyStats { Caution = 1f }, new Rng(3));
            var hurt = Enumerable.Range(0, 1000).Count(_ => mind.Decide(Senses(health: 0.2f)) == EnemyIntent.Retreat);
            var healthy = Enumerable.Range(0, 1000).Count(_ => mind.Decide(Senses(health: 1f)) == EnemyIntent.Retreat);
            Assert.True(hurt > 100);
            Assert.Equal(0, healthy);
        }

        [Fact]
        public void Buff_cast_holds_still_then_raises_damage_and_tempo_until_it_wears_off()
        {
            var stats = new EnemyStats { Aggression = 0f, Caution = 1f, Reach = 1f };
            EnemyMind mind = null;
            for (uint seed = 1; seed < 500 && mind == null; seed++)
            {
                var m = new EnemyMind(stats, new Rng(seed));
                m.Tick(0.01f, Senses(distance: 4f, health: 0.3f));
                if (m.Intent == EnemyIntent.Buff) mind = m;
            }
            Assert.NotNull(mind);
            Assert.True(mind.Steer(Senses()).Still);
            Assert.False(mind.MayAttack(Senses(distance: 0.5f)));
            Assert.False(mind.Buffed);

            var applied = 0;
            mind.BuffApplied += () => applied++;
            mind.Tick(BattleConfig.EnemyBuffCastSeconds + 0.01f, Senses(distance: 4f));
            Assert.Equal(1, applied);
            Assert.True(mind.Buffed);
            Assert.Equal(BattleConfig.EnemyBuffDamageMultiplier, mind.DamageMultiplier);
            Assert.Equal(BattleConfig.EnemyBuffTempo, mind.Tempo);
            Assert.DoesNotContain(EnemyIntent.Buff, Enumerable.Range(0, 300).Select(_ => mind.Decide(Senses(distance: 4f)))); // no re-buff while buffed

            for (var t = 0f; t < BattleConfig.EnemyBuffSeconds + 0.1f; t += 0.1f) mind.Tick(0.1f, Senses(distance: 4f));
            Assert.False(mind.Buffed);
            Assert.Equal(1f, mind.DamageMultiplier);
        }

        [Fact]
        public void Flanker_waits_for_the_blind_side_then_seizes_the_opening()
        {
            EnemyMind mind = null;
            for (uint seed = 1; seed < 500 && mind == null; seed++)
            {
                var m = new EnemyMind(new EnemyStats { Caution = 1f }, new Rng(seed));
                m.Tick(0.01f, Senses(distance: 2f, facing: 1f));
                if (m.Intent == EnemyIntent.Flank) mind = m;
            }
            Assert.NotNull(mind);
            Assert.True(mind.Steer(Senses()).TowardBack);
            Assert.False(mind.MayAttack(Senses(distance: 0.5f, facing: 1f))); // still in front of her gun

            mind.Tick(0.05f, Senses(distance: 2f, facing: -0.5f)); // reached her back
            Assert.Equal(EnemyIntent.Approach, mind.Intent);
            Assert.True(mind.MayAttack(Senses(distance: 0.5f, facing: -0.5f)));
        }

        [Fact]
        public void Mind_replays_identically_from_its_seed()
        {
            List<EnemyIntent> Run(uint seed)
            {
                var mind = new EnemyMind(new EnemyStats(), new Rng(seed));
                var log = new List<EnemyIntent>();
                for (var i = 0; i < 300; i++)
                {
                    mind.Tick(0.1f, Senses(distance: 1f + i % 5, health: 1f - i / 400f, facing: (i % 7) / 3f - 1f));
                    log.Add(mind.Intent);
                }
                return log;
            }
            Assert.Equal(Run(9), Run(9));
            Assert.Contains(EnemyIntent.Think, Run(9));
            Assert.True(Run(9).Distinct().Count() >= 4);
        }
    }
}

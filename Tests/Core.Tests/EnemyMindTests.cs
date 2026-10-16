using System.Collections.Generic;
using System.Linq;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using NUnit.Framework;

namespace UnityEve.Core.Tests
{
    public class EnemyMindTests
    {
        private static EnemySenses Senses(float distance = 4.5f, float health = 1f, float facing = 1f, int pack = 2) =>
            new EnemySenses { Distance = distance, HealthFraction = health, PlayerFacing = facing, PackAlive = pack };

        private static EnemyMind Waiting(EnemyStats stats, uint seed) => new EnemyMind(stats, new Rng(seed), 0f, () => false, () => { });

        [Test]
        public void Mind_stops_to_think_first_and_freezes_while_the_world_is_paused()
        {
            var mind = new EnemyMind(new EnemyStats(), new Rng(1), firstThinkSeconds: 0.5f);
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Think));
            Assert.That(mind.MayAttack(Senses(distance: 0.5f)), Is.False);
            Assert.That(mind.Steer(Senses()).Still, Is.True);

            mind.Tick(0f, Senses());
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Think));

            mind.Tick(0.6f, Senses());
            Assert.That(mind.Intent, Is.Not.EqualTo(EnemyIntent.Think));
        }

        [Test]
        public void Without_a_token_a_creature_never_engages_it_lurks_circles_buffs_or_backs_off()
        {
            var mind = Waiting(new EnemyStats { Aggression = 1f }, 5);
            var seen = new HashSet<EnemyIntent>();
            for (var i = 0; i < 2000; i++)
            {
                mind.Tick(0.05f, Senses(distance: 4.5f));
                seen.Add(mind.Intent);
                Assert.That(mind.MayAttack(Senses(distance: 0.5f)), Is.False);
            }
            Assert.That(seen, Does.Not.Contain(EnemyIntent.Stalk));
            Assert.That(seen, Does.Not.Contain(EnemyIntent.Flank));
            Assert.That(seen, Does.Not.Contain(EnemyIntent.Charge));
            Assert.That(seen, Does.Contain(EnemyIntent.Lurk));
            Assert.That(seen, Does.Contain(EnemyIntent.Circle));
            Assert.That(seen, Does.Contain(EnemyIntent.Think));
        }

        [Test]
        public void A_waiting_creature_that_crowds_the_player_backs_off_to_its_ring()
        {
            var mind = Waiting(new EnemyStats(), 11);
            for (var i = 0; i < 200 && (mind.Intent == EnemyIntent.Think || mind.Intent == EnemyIntent.Buff); i++) mind.Tick(0.05f, Senses());
            Assert.That(mind.Intent == EnemyIntent.Lurk || mind.Intent == EnemyIntent.Circle || mind.Intent == EnemyIntent.Retreat, Is.True);
            mind.Tick(0.05f, Senses(distance: 1f));
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Retreat));
            Assert.That(mind.Steer(Senses(distance: 1f)).RingDistance >= BattleConfig.EnemyRingMinRadius, Is.True);
        }

        [Test]
        public void Token_holder_stalks_in_rears_up_charges_strikes_then_retreats_and_hands_the_token_back()
        {
            var released = 0;
            var mind = new EnemyMind(new EnemyStats { Aggression = 1f, Caution = 0f, Reach = 1f }, new Rng(2), 0f, () => true, () => released++);
            for (var i = 0; i < 400 && !mind.HasToken; i++) mind.Tick(0.05f, Senses(distance: 5f, facing: 0f));
            Assert.That(mind.HasToken, Is.True);
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Stalk));
            Assert.That(mind.MayAttack(Senses(distance: 0.5f)), Is.False);

            mind.Tick(0.05f, Senses(distance: 2f)); // within charging distance
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Charge));
            Assert.That(mind.RearingUp, Is.True);
            Assert.That(mind.Steer(Senses()).Still, Is.True);
            Assert.That(mind.MayAttack(Senses(distance: 0.5f)), Is.False);

            mind.Tick(BattleConfig.EnemyChargeAnticipationSeconds + 0.01f, Senses(distance: 2f));
            Assert.That(mind.RearingUp, Is.False);
            Assert.That(mind.Steer(Senses()).Speed, Is.EqualTo(BattleConfig.EnemyChargeSpeed));
            Assert.That(mind.MayAttack(Senses(distance: 0.9f)), Is.True);

            mind.Struck();
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Retreat));
            Assert.That(mind.HasToken, Is.False);
            Assert.That(released, Is.EqualTo(1));
            Assert.That(mind.MayAttack(Senses(distance: 0.5f)), Is.False);
        }

        [Test]
        public void A_charge_that_never_connects_gives_up_the_turn()
        {
            var released = 0;
            var mind = new EnemyMind(new EnemyStats { Aggression = 1f, Reach = 1f }, new Rng(4), 0f, () => true, () => released++);
            for (var i = 0; i < 400 && mind.Intent != EnemyIntent.Charge; i++) mind.Tick(0.05f, Senses(distance: 2f, facing: 0f));
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Charge));
            mind.Tick(BattleConfig.EnemyChargeAnticipationSeconds + BattleConfig.EnemyChargeMaxSeconds + 0.1f, Senses(distance: 6f));
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Retreat));
            Assert.That(released, Is.EqualTo(1));
        }

        [Test]
        public void Attack_tokens_let_one_creature_engage_at_a_time_with_a_breather_between_turns()
        {
            var tokens = new AttackTokens(AttackTokens.CapacityFor(3), new Rng(1));
            object a = "a", b = "b";
            Assert.That(tokens.TryTake(a), Is.True);
            Assert.That(tokens.TryTake(b), Is.False);
            tokens.Release(a);
            Assert.That(tokens.TryTake(b), Is.False); // breather
            tokens.Tick(0f);                 // paused world: the breather does not run
            Assert.That(tokens.Available, Is.False);
            tokens.Tick(BattleConfig.TokenBreatherMaxSeconds + 0.01f);
            Assert.That(tokens.TryTake(b), Is.True);
            Assert.That(AttackTokens.CapacityFor(4), Is.EqualTo(2));
        }

        [Test]
        public void Temperament_shapes_choices_aggressive_seize_turns_cautious_hang_back()
        {
            int Turns(EnemyStats stats)
            {
                var mind = new EnemyMind(stats, new Rng(42), 0f, () => true, () => { });
                return Enumerable.Range(0, 2000).Count(_ => mind.Decide(Senses()) is EnemyIntent.Stalk or EnemyIntent.Flank or EnemyIntent.Charge);
            }
            int Lurks(EnemyStats stats)
            {
                var mind = Waiting(stats, 42);
                return Enumerable.Range(0, 2000).Count(_ => mind.Decide(Senses()) == EnemyIntent.Lurk);
            }
            var brute = new EnemyStats { Aggression = 1f, Caution = 0f };
            var coward = new EnemyStats { Aggression = 0f, Caution = 1f };
            Assert.That(Turns(brute) > 2 * Turns(coward), Is.True);
            Assert.That(Lurks(coward) > Lurks(brute), Is.True);
        }

        [Test]
        public void Hurt_cautious_creatures_retreat_and_healthy_ones_do_not()
        {
            var mind = Waiting(new EnemyStats { Caution = 1f }, 3);
            var hurt = Enumerable.Range(0, 1000).Count(_ => mind.Decide(Senses(health: 0.2f)) == EnemyIntent.Retreat);
            var healthy = Enumerable.Range(0, 1000).Count(_ => mind.Decide(Senses(health: 1f)) == EnemyIntent.Retreat);
            Assert.That(hurt > 100, Is.True);
            Assert.That(healthy, Is.EqualTo(0));
        }

        [Test]
        public void Buff_cast_holds_still_then_raises_damage_and_tempo_until_it_wears_off()
        {
            var stats = new EnemyStats { Aggression = 0f, Caution = 1f, Reach = 1f };
            EnemyMind mind = null;
            for (uint seed = 1; seed < 500 && mind == null; seed++)
            {
                var m = Waiting(stats, seed);
                m.Tick(0.01f, Senses(health: 0.3f));
                if (m.Intent == EnemyIntent.Buff) mind = m;
            }
            Assert.That(mind, Is.Not.Null);
            Assert.That(mind.Steer(Senses()).Still, Is.True);
            Assert.That(mind.Buffed, Is.False);

            var applied = 0;
            mind.BuffApplied += () => applied++;
            mind.Tick(BattleConfig.EnemyBuffCastSeconds + 0.01f, Senses());
            Assert.That(applied, Is.EqualTo(1));
            Assert.That(mind.Buffed, Is.True);
            Assert.That(mind.DamageMultiplier, Is.EqualTo(BattleConfig.EnemyBuffDamageMultiplier));
            Assert.That(mind.Tempo, Is.EqualTo(BattleConfig.EnemyBuffTempo));
            Assert.That(Enumerable.Range(0, 300).Select(_ => mind.Decide(Senses())), Does.Not.Contain(EnemyIntent.Buff)); // no re-buff while buffed

            for (var t = 0f; t < BattleConfig.EnemyBuffSeconds + 0.1f; t += 0.1f) mind.Tick(0.1f, Senses());
            Assert.That(mind.Buffed, Is.False);
        }

        [Test]
        public void Flanker_works_round_then_charges_from_the_blind_side()
        {
            EnemyMind mind = null;
            for (uint seed = 1; seed < 500 && mind == null; seed++)
            {
                var m = new EnemyMind(new EnemyStats { Caution = 1f, Aggression = 1f }, new Rng(seed), 0f, () => true, () => { });
                m.Tick(0.01f, Senses(distance: 5f, facing: 1f));
                if (m.Intent == EnemyIntent.Flank) mind = m;
            }
            Assert.That(mind, Is.Not.Null);
            Assert.That(mind.Steer(Senses()).TowardBack, Is.True);
            mind.Tick(0.05f, Senses(distance: 3f, facing: -0.5f)); // reached her back
            Assert.That(mind.Intent, Is.EqualTo(EnemyIntent.Charge));
        }

        [Test]
        public void Mind_replays_identically_from_its_seed()
        {
            List<EnemyIntent> Run(uint seed)
            {
                var tokens = new AttackTokens(1, new Rng(seed));
                var me = new object();
                var mind = new EnemyMind(new EnemyStats(), new Rng(seed), 0f, () => tokens.TryTake(me), () => tokens.Release(me));
                var log = new List<EnemyIntent>();
                for (var i = 0; i < 600; i++)
                {
                    tokens.Tick(0.05f);
                    mind.Tick(0.05f, Senses(distance: 1f + i % 6, health: 1f - i / 800f, facing: (i % 7) / 3f - 1f));
                    if (mind.Intent == EnemyIntent.Charge && !mind.RearingUp && i % 9 == 0) mind.Struck();
                    log.Add(mind.Intent);
                }
                return log;
            }
            Assert.That(Run(9), Is.EqualTo(Run(9)));
            Assert.That(Run(9).Distinct().Count() >= 5, Is.True);
        }
    }
}

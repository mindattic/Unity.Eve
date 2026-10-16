using System.Linq;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using NUnit.Framework;

namespace UnityEve.Core.Tests
{
    public class BattleRulesTests
    {
        [Test]
        public void Stagger_triggers_at_the_floor_for_low_health_creatures()
        {
            Assert.That(StaggerRule.ShouldStagger(BattleConfig.StaggerDamageFloor - 1f, maxHp: 10f), Is.False);
            Assert.That(StaggerRule.ShouldStagger(BattleConfig.StaggerDamageFloor, maxHp: 10f), Is.True);
        }

        [Test]
        public void Stagger_triggers_at_a_fraction_of_max_hp_for_tanky_creatures()
        {
            var maxHp = 200f;
            var threshold = maxHp * BattleConfig.StaggerDamageFraction;
            Assert.That(StaggerRule.ShouldStagger(threshold - 1f, maxHp), Is.False);
            Assert.That(StaggerRule.ShouldStagger(threshold, maxHp), Is.True);
        }

        [Test]
        public void Atb_fills_in_base_time_at_base_rate()
        {
            var atb = new AtbGauge();
            var rate = AtbGauge.Rate(activeTime: 0, atDownPenalty: 0, weaponMultiplier: 1f);
            atb.Tick(BattleConfig.BaseAtbSeconds - 0.05f, rate);
            Assert.That(atb.IsFull, Is.False);
            atb.Tick(0.1f, rate);
            Assert.That(atb.IsFull, Is.True);
        }

        [Test]
        public void Atb_rate_follows_pe1_formula_and_floor()
        {
            Assert.That(AtbGauge.Rate(0, 0, 1f), Is.EqualTo(50f));
            Assert.That(AtbGauge.Rate(0, 0, 0.8f), Is.EqualTo(40f));          // shotgun
            Assert.That(AtbGauge.Rate(0, 0, 2f), Is.EqualTo(100f));           // club
            Assert.That(AtbGauge.Rate(0, 40, 0.4f), Is.EqualTo(BattleConfig.AtbMinimumRate));
        }

        [Test]
        public void Atb_does_not_fill_while_world_is_paused()
        {
            var atb = new AtbGauge();
            atb.Tick(0f, 50f);
            Assert.That(atb.Value, Is.EqualTo(0f));
        }

        [Test]
        public void Atb_filled_event_fires_once_per_fill()
        {
            var atb = new AtbGauge();
            var fired = 0;
            atb.Filled += () => fired++;
            for (var i = 0; i < 100; i++) atb.Tick(0.1f, 50f);
            Assert.That(fired, Is.EqualTo(1));
            atb.Spend();
            for (var i = 0; i < 100; i++) atb.Tick(0.1f, 50f);
            Assert.That(fired, Is.EqualTo(2));
        }

        [Test]
        public void Haste_doubles_atb_fill()
        {
            var normal = new AtbGauge();
            var hasted = new AtbGauge();
            normal.Tick(1f, 50f);
            hasted.Tick(1f, 50f, hasted: true);
            Assert.That(hasted.Value, Is.EqualTo(normal.Value * 2f).Within(0.0005));
        }

        [Test]
        public void Each_pe_cast_slows_pe_regeneration()
        {
            var pe = new ParasiteEnergy(100f);
            pe.BeginBattle();
            Assert.That(pe.TrySpend(50f), Is.True);
            var afterOne = pe.RegenMultiplier;
            Assert.That(pe.TrySpend(20f), Is.True);
            Assert.That(pe.RegenMultiplier < afterOne, Is.True);
            for (var i = 0; i < 20; i++) pe.TrySpend(0f);
            Assert.That(pe.RegenMultiplier, Is.EqualTo(BattleConfig.PeRegenFloor));
        }

        [Test]
        public void Pe_cannot_be_overspent()
        {
            var pe = new ParasiteEnergy(30f);
            Assert.That(pe.TrySpend(31f), Is.False);
            Assert.That(pe.Current, Is.EqualTo(30f));
        }

        [Test]
        public void Rate_of_fire_trades_per_round_power_for_total_power()
        {
            Assert.That(DamageCalculator.RoundShare(1), Is.EqualTo(1f));
            Assert.That(DamageCalculator.RoundShare(2) * 2, Is.EqualTo(1.2f).Within(0.0005));
            Assert.That(DamageCalculator.RoundShare(3) * 3 > DamageCalculator.RoundShare(2) * 2, Is.True);
        }

        [Test]
        public void Single_target_weapon_puts_every_round_on_the_chosen_target()
        {
            var w = new WeaponStats { RoundsPerTurn = 3 };
            var plan = DamageCalculator.PlanRounds(w, chosenTarget: 1, targetsInRange: 4, roundsAvailable: 10);
            Assert.That(plan, Is.EqualTo(new[] { 1, 1, 1 }));
        }

        [Test]
        public void Burst_weapon_hits_every_target_in_range()
        {
            var w = new WeaponStats { RoundsPerTurn = 1, Burst = true };
            var plan = DamageCalculator.PlanRounds(w, chosenTarget: 2, targetsInRange: 3, roundsAvailable: 10);
            Assert.That(plan, Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void Rounds_are_limited_by_ammo_in_the_clip()
        {
            var w = new WeaponStats { RoundsPerTurn = 3 };
            Assert.That(DamageCalculator.PlanRounds(w, 0, 1, roundsAvailable: 2).Count, Is.EqualTo(2));
        }

        [Test]
        public void Damage_never_drops_below_one_and_replays_from_seed()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (var i = 0; i < 50; i++)
            {
                var ha = DamageCalculator.Roll(5, 1f, 100, a);
                var hb = DamageCalculator.Roll(5, 1f, 100, b);
                Assert.That(ha.Amount, Is.EqualTo(1));
                Assert.That(hb.Amount, Is.EqualTo(ha.Amount));
            }
        }

        [Test]
        public void Enemy_runs_close_windup_strike_recover_and_respects_commit_limit()
        {
            var stats = new EnemyStats { Reach = 1f, WindupSeconds = 0.4f, RecoverSeconds = 0.3f, CooldownSeconds = 1f };
            var cycle = new EnemyAttackCycle(stats, initialCooldown: 0f);
            var strikes = 0;
            cycle.StrikeLanded += () => strikes++;

            cycle.Tick(0.1f, distanceToPlayer: 0.8f, mayCommit: false);
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Close));

            cycle.Tick(0.1f, 0.8f, mayCommit: true);
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Windup));

            cycle.Tick(0f, 0.8f, true); // paused world: frozen mid-telegraph
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Windup));

            cycle.Tick(0.45f, 0.8f, true);
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Strike));
            cycle.Tick(0.2f, 0.8f, true);
            Assert.That(strikes, Is.EqualTo(1));
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Recover));
            cycle.Tick(0.35f, 0.8f, true);
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Close));
            Assert.That(cycle.Cooldown, Is.EqualTo(1f));
        }

        [Test]
        public void Interrupt_cancels_a_windup()
        {
            var cycle = new EnemyAttackCycle(new EnemyStats { Reach = 1f }, 0f);
            cycle.Tick(0.1f, 0.5f, true);
            Assert.That(cycle.IsCommitted, Is.True);
            cycle.Interrupt();
            Assert.That(cycle.Phase, Is.EqualTo(AttackPhase.Close));
            Assert.That(cycle.Cooldown >= 0.35f, Is.True);
        }

        [TestCase(3f, false, StrikeOutcome.Whiff)]
        [TestCase(1f, true, StrikeOutcome.Evaded)]
        [TestCase(1f, false, StrikeOutcome.Hit)]
        public void Strike_resolution(float distance, bool invulnerable, StrikeOutcome expected)
        {
            Assert.That(EnemyAttackCycle.Resolve(distance, reach: 1f, invulnerable), Is.EqualTo(expected));
        }

        [Test]
        public void Perfect_dodge_window_opens_focus_and_slows_time()
        {
            Assert.That(FocusWindow.IsPerfect(0.1f), Is.True);
            Assert.That(FocusWindow.IsPerfect(0.45f), Is.False);

            var focus = new FocusWindow();
            focus.Open();
            Assert.That(focus.IsOpen, Is.True);
            Assert.That(focus.TimeScale, Is.EqualTo(FocusWindow.SlowTimeScale));
            Assert.That(focus.TryUse(FocusMove.QuickHeal), Is.True);
            Assert.That(focus.IsOpen, Is.False);
            Assert.That(focus.TryUse(FocusMove.TargetVitals), Is.False);
        }

        [Test]
        public void Focus_window_expires_on_real_time_so_it_is_optional()
        {
            var focus = new FocusWindow();
            focus.Open();
            focus.Tick(FocusWindow.DurationRealSeconds + 0.01f);
            Assert.That(focus.IsOpen, Is.False);
            Assert.That(focus.TimeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Level_up_grants_bonus_points_and_bp_raise_active_time()
        {
            var p = new Progression();
            var levels = p.AddExp(Progression.ExpForLevel(1) + Progression.ExpForLevel(2));
            Assert.That(levels, Is.EqualTo(2));
            Assert.That(p.Level, Is.EqualTo(3));
            Assert.That(p.BonusPoints, Is.EqualTo(2 * BattleConfig.BonusPointsPerLevel));
            Assert.That(p.TrySpendOnActiveTime(2), Is.True);
            Assert.That(p.ActiveTime, Is.EqualTo(4));
        }

        [Test]
        public void Escape_chance_is_clamped_and_favours_stronger_players()
        {
            Assert.That(EscapeRule.Chance(5, 1) > EscapeRule.Chance(1, 5), Is.True);
            Assert.That(EscapeRule.Chance(50, 1), Is.EqualTo(BattleConfig.EscapeMaxChance));
            Assert.That(EscapeRule.Chance(1, 50), Is.EqualTo(BattleConfig.EscapeMinChance));
        }

        [Test]
        public void Health_clamps_damage_and_healing()
        {
            var h = new Health(50);
            Assert.That(h.Damage(80), Is.EqualTo(50));
            Assert.That(h.IsDead, Is.True);
            Assert.That(h.Heal(10), Is.EqualTo(0)); // the dead are not healed by medicine
            h.Revive();
            Assert.That(new[] { h.Current }.ToArray(), Is.EqualTo(new[] { 50 }));
        }

        [Test]
        public void ActiveReload_sweet_spot_sits_clear_of_both_ends_and_replays_from_seed()
        {
            var a = new ActiveReload(1.2f, new Rng(7));
            var b = new ActiveReload(1.2f, new Rng(7));
            Assert.That(b.SweetSpotStart, Is.EqualTo(a.SweetSpotStart));
            Assert.That(a.SweetSpotStart > 0f, Is.True);
            Assert.That(a.SweetSpotEnd < a.Duration, Is.True);
            Assert.That(a.SweetSpotEnd - a.SweetSpotStart, Is.EqualTo(a.Duration * BattleConfig.ActiveReloadSweetSpotWidth).Within(0.0005));
        }

        [Test]
        public void ActiveReload_is_only_in_the_sweet_spot_inside_its_window_and_finishes_at_its_duration()
        {
            var reload = new ActiveReload(1.2f, new Rng(7));
            reload.Tick(reload.SweetSpotStart - 0.01f);
            Assert.That(reload.InSweetSpot, Is.False);
            Assert.That(reload.Finished, Is.False);
            reload.Tick(0.02f);
            Assert.That(reload.InSweetSpot, Is.True);
            reload.Tick(1f);
            Assert.That(reload.Finished, Is.True);
            Assert.That(reload.InSweetSpot, Is.False);
        }
    }
}

using System.Linq;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using Xunit;

namespace UnityEve.Core.Tests
{
    public class BattleRulesTests
    {
        [Fact]
        public void Stagger_triggers_at_the_floor_for_low_health_creatures()
        {
            Assert.False(StaggerRule.ShouldStagger(BattleConfig.StaggerDamageFloor - 1f, maxHp: 10f));
            Assert.True(StaggerRule.ShouldStagger(BattleConfig.StaggerDamageFloor, maxHp: 10f));
        }

        [Fact]
        public void Stagger_triggers_at_a_fraction_of_max_hp_for_tanky_creatures()
        {
            var maxHp = 200f;
            var threshold = maxHp * BattleConfig.StaggerDamageFraction;
            Assert.False(StaggerRule.ShouldStagger(threshold - 1f, maxHp));
            Assert.True(StaggerRule.ShouldStagger(threshold, maxHp));
        }

        [Fact]
        public void Atb_fills_in_base_time_at_base_rate()
        {
            var atb = new AtbGauge();
            var rate = AtbGauge.Rate(activeTime: 0, atDownPenalty: 0, weaponMultiplier: 1f);
            atb.Tick(BattleConfig.BaseAtbSeconds - 0.05f, rate);
            Assert.False(atb.IsFull);
            atb.Tick(0.1f, rate);
            Assert.True(atb.IsFull);
        }

        [Fact]
        public void Atb_rate_follows_pe1_formula_and_floor()
        {
            Assert.Equal(50f, AtbGauge.Rate(0, 0, 1f));
            Assert.Equal(40f, AtbGauge.Rate(0, 0, 0.8f));          // shotgun
            Assert.Equal(100f, AtbGauge.Rate(0, 0, 2f));           // club
            Assert.Equal(BattleConfig.AtbMinimumRate, AtbGauge.Rate(0, 40, 0.4f));
        }

        [Fact]
        public void Atb_does_not_fill_while_world_is_paused()
        {
            var atb = new AtbGauge();
            atb.Tick(0f, 50f);
            Assert.Equal(0f, atb.Value);
        }

        [Fact]
        public void Atb_filled_event_fires_once_per_fill()
        {
            var atb = new AtbGauge();
            var fired = 0;
            atb.Filled += () => fired++;
            for (var i = 0; i < 100; i++) atb.Tick(0.1f, 50f);
            Assert.Equal(1, fired);
            atb.Spend();
            for (var i = 0; i < 100; i++) atb.Tick(0.1f, 50f);
            Assert.Equal(2, fired);
        }

        [Fact]
        public void Haste_doubles_atb_fill()
        {
            var normal = new AtbGauge();
            var hasted = new AtbGauge();
            normal.Tick(1f, 50f);
            hasted.Tick(1f, 50f, hasted: true);
            Assert.Equal(normal.Value * 2f, hasted.Value, 3);
        }

        [Fact]
        public void Each_pe_cast_slows_pe_regeneration()
        {
            var pe = new ParasiteEnergy(100f);
            pe.BeginBattle();
            Assert.True(pe.TrySpend(50f));
            var afterOne = pe.RegenMultiplier;
            Assert.True(pe.TrySpend(20f));
            Assert.True(pe.RegenMultiplier < afterOne);
            for (var i = 0; i < 20; i++) pe.TrySpend(0f);
            Assert.Equal(BattleConfig.PeRegenFloor, pe.RegenMultiplier);
        }

        [Fact]
        public void Pe_cannot_be_overspent()
        {
            var pe = new ParasiteEnergy(30f);
            Assert.False(pe.TrySpend(31f));
            Assert.Equal(30f, pe.Current);
        }

        [Fact]
        public void Rate_of_fire_trades_per_round_power_for_total_power()
        {
            Assert.Equal(1f, DamageCalculator.RoundShare(1));
            Assert.Equal(1.2f, DamageCalculator.RoundShare(2) * 2, 3);
            Assert.True(DamageCalculator.RoundShare(3) * 3 > DamageCalculator.RoundShare(2) * 2);
        }

        [Fact]
        public void Single_target_weapon_puts_every_round_on_the_chosen_target()
        {
            var w = new WeaponStats { RoundsPerTurn = 3 };
            var plan = DamageCalculator.PlanRounds(w, chosenTarget: 1, targetsInRange: 4, roundsAvailable: 10);
            Assert.Equal(new[] { 1, 1, 1 }, plan);
        }

        [Fact]
        public void Burst_weapon_hits_every_target_in_range()
        {
            var w = new WeaponStats { RoundsPerTurn = 1, Burst = true };
            var plan = DamageCalculator.PlanRounds(w, chosenTarget: 2, targetsInRange: 3, roundsAvailable: 10);
            Assert.Equal(new[] { 2, 0, 1 }, plan);
        }

        [Fact]
        public void Rounds_are_limited_by_ammo_in_the_clip()
        {
            var w = new WeaponStats { RoundsPerTurn = 3 };
            Assert.Equal(2, DamageCalculator.PlanRounds(w, 0, 1, roundsAvailable: 2).Count);
        }

        [Fact]
        public void Damage_never_drops_below_one_and_replays_from_seed()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (var i = 0; i < 50; i++)
            {
                var ha = DamageCalculator.Roll(5, 1f, 100, a);
                var hb = DamageCalculator.Roll(5, 1f, 100, b);
                Assert.Equal(1, ha.Amount);
                Assert.Equal(ha.Amount, hb.Amount);
            }
        }

        [Fact]
        public void Enemy_runs_close_windup_strike_recover_and_respects_commit_limit()
        {
            var stats = new EnemyStats { Reach = 1f, WindupSeconds = 0.4f, RecoverSeconds = 0.3f, CooldownSeconds = 1f };
            var cycle = new EnemyAttackCycle(stats, initialCooldown: 0f);
            var strikes = 0;
            cycle.StrikeLanded += () => strikes++;

            cycle.Tick(0.1f, distanceToPlayer: 0.8f, mayCommit: false);
            Assert.Equal(AttackPhase.Close, cycle.Phase);

            cycle.Tick(0.1f, 0.8f, mayCommit: true);
            Assert.Equal(AttackPhase.Windup, cycle.Phase);

            cycle.Tick(0f, 0.8f, true); // paused world: frozen mid-telegraph
            Assert.Equal(AttackPhase.Windup, cycle.Phase);

            cycle.Tick(0.45f, 0.8f, true);
            Assert.Equal(AttackPhase.Strike, cycle.Phase);
            cycle.Tick(0.2f, 0.8f, true);
            Assert.Equal(1, strikes);
            Assert.Equal(AttackPhase.Recover, cycle.Phase);
            cycle.Tick(0.35f, 0.8f, true);
            Assert.Equal(AttackPhase.Close, cycle.Phase);
            Assert.Equal(1f, cycle.Cooldown);
        }

        [Fact]
        public void Interrupt_cancels_a_windup()
        {
            var cycle = new EnemyAttackCycle(new EnemyStats { Reach = 1f }, 0f);
            cycle.Tick(0.1f, 0.5f, true);
            Assert.True(cycle.IsCommitted);
            cycle.Interrupt();
            Assert.Equal(AttackPhase.Close, cycle.Phase);
            Assert.True(cycle.Cooldown >= 0.35f);
        }

        [Theory]
        [InlineData(3f, false, StrikeOutcome.Whiff)]
        [InlineData(1f, true, StrikeOutcome.Evaded)]
        [InlineData(1f, false, StrikeOutcome.Hit)]
        public void Strike_resolution(float distance, bool invulnerable, StrikeOutcome expected)
        {
            Assert.Equal(expected, EnemyAttackCycle.Resolve(distance, reach: 1f, invulnerable));
        }

        [Fact]
        public void Perfect_dodge_window_opens_focus_and_slows_time()
        {
            Assert.True(FocusWindow.IsPerfect(0.1f));
            Assert.False(FocusWindow.IsPerfect(0.45f));

            var focus = new FocusWindow();
            focus.Open();
            Assert.True(focus.IsOpen);
            Assert.Equal(FocusWindow.SlowTimeScale, focus.TimeScale);
            Assert.True(focus.TryUse(FocusMove.QuickHeal));
            Assert.False(focus.IsOpen);
            Assert.False(focus.TryUse(FocusMove.TargetVitals));
        }

        [Fact]
        public void Focus_window_expires_on_real_time_so_it_is_optional()
        {
            var focus = new FocusWindow();
            focus.Open();
            focus.Tick(FocusWindow.DurationRealSeconds + 0.01f);
            Assert.False(focus.IsOpen);
            Assert.Equal(1f, focus.TimeScale);
        }

        [Fact]
        public void Level_up_grants_bonus_points_and_bp_raise_active_time()
        {
            var p = new Progression();
            var levels = p.AddExp(Progression.ExpForLevel(1) + Progression.ExpForLevel(2));
            Assert.Equal(2, levels);
            Assert.Equal(3, p.Level);
            Assert.Equal(2 * BattleConfig.BonusPointsPerLevel, p.BonusPoints);
            Assert.True(p.TrySpendOnActiveTime(2));
            Assert.Equal(4, p.ActiveTime);
        }

        [Fact]
        public void Escape_chance_is_clamped_and_favours_stronger_players()
        {
            Assert.True(EscapeRule.Chance(5, 1) > EscapeRule.Chance(1, 5));
            Assert.Equal(BattleConfig.EscapeMaxChance, EscapeRule.Chance(50, 1));
            Assert.Equal(BattleConfig.EscapeMinChance, EscapeRule.Chance(1, 50));
        }

        [Fact]
        public void Health_clamps_damage_and_healing()
        {
            var h = new Health(50);
            Assert.Equal(50, h.Damage(80));
            Assert.True(h.IsDead);
            Assert.Equal(0, h.Heal(10)); // the dead are not healed by medicine
            h.Revive();
            Assert.Equal(new[] { 50 }, new[] { h.Current }.ToArray());
        }

        [Fact]
        public void ActiveReload_sweet_spot_sits_clear_of_both_ends_and_replays_from_seed()
        {
            var a = new ActiveReload(1.2f, new Rng(7));
            var b = new ActiveReload(1.2f, new Rng(7));
            Assert.Equal(a.SweetSpotStart, b.SweetSpotStart);
            Assert.True(a.SweetSpotStart > 0f);
            Assert.True(a.SweetSpotEnd < a.Duration);
            Assert.Equal(a.Duration * BattleConfig.ActiveReloadSweetSpotWidth, a.SweetSpotEnd - a.SweetSpotStart, 3);
        }

        [Fact]
        public void ActiveReload_is_only_in_the_sweet_spot_inside_its_window_and_finishes_at_its_duration()
        {
            var reload = new ActiveReload(1.2f, new Rng(7));
            reload.Tick(reload.SweetSpotStart - 0.01f);
            Assert.False(reload.InSweetSpot);
            Assert.False(reload.Finished);
            reload.Tick(0.02f);
            Assert.True(reload.InSweetSpot);
            reload.Tick(1f);
            Assert.True(reload.Finished);
            Assert.False(reload.InSweetSpot);
        }
    }
}

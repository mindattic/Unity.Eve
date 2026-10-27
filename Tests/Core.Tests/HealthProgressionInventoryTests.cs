using System.Linq;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Items;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>
    /// Edge cases of the counters that save restore and level-up lean on: a silent clamping bug here
    /// corrupts a save rather than failing loudly.
    /// </summary>
    public class HealthProgressionInventoryTests
    {
        // --- Health -------------------------------------------------------------------------

        [Test]
        public void Health_max_is_never_below_one()
        {
            Assert.That(new Health(0).Max, Is.EqualTo(1));
            var hp = new Health(10);
            hp.SetMax(-5, refill: false);
            Assert.That(hp.Max, Is.EqualTo(1));
            Assert.That(hp.Current, Is.EqualTo(1));
        }

        [Test]
        public void SetMax_refill_fills_and_without_refill_only_clamps_down()
        {
            var hp = new Health(100);
            hp.Damage(20);
            hp.SetMax(150, refill: false);
            Assert.That(hp.Current, Is.EqualTo(80), "raising max without refill keeps current HP");
            hp.SetMax(50, refill: false);
            Assert.That(hp.Current, Is.EqualTo(50), "lowering max clamps current HP down");
            hp.Damage(30);
            hp.SetMax(120, refill: true);
            Assert.That(hp.Current, Is.EqualTo(120));
        }

        [Test]
        public void The_dead_cannot_be_healed_until_revived()
        {
            var hp = new Health(40);
            Assert.That(hp.Damage(999), Is.EqualTo(40));
            Assert.That(hp.IsDead, Is.True);
            Assert.That(hp.Heal(10), Is.EqualTo(0));
            Assert.That(hp.Current, Is.EqualTo(0));
            hp.Revive();
            Assert.That(hp.Current, Is.EqualTo(40));
        }

        [TestCase(-5, 1)]
        [TestCase(0, 1)]
        [TestCase(25, 25)]
        [TestCase(999, 60)]
        public void Restore_clamps_to_one_through_max(int value, int expected)
        {
            var hp = new Health(60);
            hp.Restore(value);
            Assert.That(hp.Current, Is.EqualTo(expected));
        }

        // --- Progression ----------------------------------------------------------------------

        [Test]
        public void ExpForLevel_follows_the_curve()
        {
            Assert.That(Progression.ExpForLevel(1), Is.EqualTo(BattleConfig.ExpPerLevelBase));
            Assert.That(Progression.ExpForLevel(2), Is.EqualTo(87));
            Assert.That(Progression.ExpForLevel(3), Is.EqualTo(126));
        }

        [Test]
        public void AddExp_can_gain_several_levels_in_one_call()
        {
            var p = new Progression();
            var gained = p.AddExp(60 + 87 + 10);
            Assert.That(gained, Is.EqualTo(2));
            Assert.That(p.Level, Is.EqualTo(3));
            Assert.That(p.Exp, Is.EqualTo(10));
            Assert.That(p.BonusPoints, Is.EqualTo(2 * BattleConfig.BonusPointsPerLevel));
        }

        [Test]
        public void AddExp_ignores_zero_and_negative()
        {
            var p = new Progression();
            Assert.That(p.AddExp(0), Is.EqualTo(0));
            Assert.That(p.AddExp(-50), Is.EqualTo(0));
            Assert.That(p.Exp, Is.EqualTo(0));
        }

        [Test]
        public void TrySpendOnActiveTime_rejects_zero_negative_and_over_budget()
        {
            var p = new Progression();
            p.Restore(level: 2, exp: 0, bonusPoints: 3, activeTime: 0);
            Assert.That(p.TrySpendOnActiveTime(0), Is.False);
            Assert.That(p.TrySpendOnActiveTime(-1), Is.False);
            Assert.That(p.TrySpendOnActiveTime(4), Is.False);
            Assert.That(p.BonusPoints, Is.EqualTo(3));
            Assert.That(p.TrySpendOnActiveTime(2), Is.True);
            Assert.That(p.BonusPoints, Is.EqualTo(1));
            Assert.That(p.ActiveTime, Is.EqualTo(4));
        }

        [Test]
        public void Progression_restore_clamps_negatives()
        {
            var p = new Progression();
            p.Restore(level: -3, exp: -1, bonusPoints: -2, activeTime: -4);
            Assert.That(p.Level, Is.EqualTo(1));
            Assert.That(p.Exp, Is.EqualTo(0));
            Assert.That(p.BonusPoints, Is.EqualTo(0));
            Assert.That(p.ActiveTime, Is.EqualTo(0));
        }

        [Test]
        public void Max_hp_and_pe_grow_with_level()
        {
            var p = new Progression();
            Assert.That(p.MaxHpForLevel, Is.EqualTo(80));
            Assert.That(p.MaxPeForLevel, Is.EqualTo(60f));
            p.Restore(3, 0, 0, 0);
            Assert.That(p.MaxHpForLevel, Is.EqualTo(108));
            Assert.That(p.MaxPeForLevel, Is.EqualTo(80f));
        }

        // --- Inventory ------------------------------------------------------------------------

        private static Inventory Bag(int capacity) => new Inventory(capacity, id => id == "grenade" ? 3 : 9);

        [Test]
        public void TryRemove_fails_without_mutating_when_short()
        {
            var bag = Bag(4);
            bag.Add("grenade", 2);
            var changed = 0;
            bag.Changed += () => changed++;
            Assert.That(bag.TryRemove("grenade", 3), Is.False);
            Assert.That(bag.TryRemove("medicine"), Is.False);
            Assert.That(bag.TryRemove("grenade", 0), Is.False);
            Assert.That(bag.CountOf("grenade"), Is.EqualTo(2));
            Assert.That(changed, Is.EqualTo(0));
        }

        [Test]
        public void TryRemove_drains_the_last_slot_first_and_drops_empty_slots()
        {
            var bag = Bag(4);
            bag.Add("grenade", 5); // [3, 2]
            Assert.That(bag.TryRemove("grenade", 1), Is.True);
            Assert.That(bag.Slots.Select(s => s.Count), Is.EqualTo(new[] { 3, 1 }));
            Assert.That(bag.TryRemove("grenade", 2), Is.True);
            Assert.That(bag.Slots.Select(s => s.Count), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void SetCapacity_never_shrinks_below_used_slots()
        {
            var bag = Bag(5);
            bag.Add("grenade", 9); // three slots
            bag.SetCapacity(1);
            Assert.That(bag.Capacity, Is.EqualTo(3));
            bag.SetCapacity(8);
            Assert.That(bag.Capacity, Is.EqualTo(8));
        }

        [Test]
        public void Add_ignores_non_positive_counts()
        {
            var bag = Bag(2);
            var changed = 0;
            bag.Changed += () => changed++;
            Assert.That(bag.Add("medicine", 0), Is.EqualTo(0));
            Assert.That(bag.Add("medicine", -3), Is.EqualTo(0));
            Assert.That(bag.Slots, Is.Empty);
            Assert.That(changed, Is.EqualTo(0));
        }

        [Test]
        public void A_max_stack_of_zero_or_less_is_treated_as_one()
        {
            var bag = new Inventory(5, _ => 0);
            Assert.That(bag.Add("key", 3), Is.EqualTo(0));
            Assert.That(bag.Slots.Count, Is.EqualTo(3));
            Assert.That(bag.Slots.All(s => s.Count == 1), Is.True);
        }

        [Test]
        public void Changed_fires_on_add_remove_and_clear()
        {
            var bag = Bag(3);
            var changed = 0;
            bag.Changed += () => changed++;
            bag.Add("medicine", 2);
            Assert.That(changed, Is.EqualTo(1));
            bag.TryRemove("medicine");
            Assert.That(changed, Is.EqualTo(2));
            bag.Clear();
            Assert.That(changed, Is.EqualTo(3));
            Assert.That(bag.Slots, Is.Empty);
        }
    }
}

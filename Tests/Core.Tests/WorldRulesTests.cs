using System;
using Unity.Eve.Core.Camera;
using Unity.Eve.Core.Items;
using Unity.Eve.Core.Util;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    public class WorldRulesTests
    {
        private static Inventory Bag(int capacity) => new Inventory(capacity, id => id == "grenade" ? 3 : 9);

        [Test]
        public void Inventory_stacks_then_spills_into_new_slots()
        {
            var bag = Bag(3);
            Assert.That(bag.Add("grenade", 5), Is.EqualTo(0));
            Assert.That(bag.Slots.Count, Is.EqualTo(2));
            Assert.That(bag.CountOf("grenade"), Is.EqualTo(5));
        }

        [Test]
        public void Inventory_reports_overflow_when_full()
        {
            var bag = Bag(1);
            Assert.That(bag.Add("grenade", 5), Is.EqualTo(2));
            Assert.That(bag.CountOf("grenade"), Is.EqualTo(3));
        }

        [Test]
        public void Inventory_remove_frees_empty_slots()
        {
            var bag = Bag(4);
            bag.Add("medicine", 2);
            Assert.That(bag.TryRemove("medicine", 2), Is.True);
            Assert.That(bag.Slots, Is.Empty);
            Assert.That(bag.TryRemove("medicine"), Is.False);
        }

        [Test]
        public void Loot_table_is_deterministic_and_can_roll_nothing()
        {
            var table = new LootTable
            {
                Pulls = 3,
                Entries =
                {
                    new LootEntry { ItemId = "medicine", Weight = 1f },
                    new LootEntry { ItemId = "", Weight = 1f },
                },
            };
            var a = table.Roll(new Rng(7));
            var b = table.Roll(new Rng(7));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(a.Count <= 3, Is.True);
        }

        [Test]
        public void Camera_selector_prefers_priority_then_most_recent_and_holds_in_gaps()
        {
            var s = new CameraZoneSelector();
            s.Enter(1, priority: 0);
            Assert.That(s.ActiveZone, Is.EqualTo(1));
            s.Enter(2, priority: 0);
            Assert.That(s.ActiveZone, Is.EqualTo(2));           // most recent wins a tie
            s.Enter(3, priority: 5);
            s.Enter(4, priority: 0);
            Assert.That(s.ActiveZone, Is.EqualTo(3));           // priority beats recency
            s.Exit(3);
            Assert.That(s.ActiveZone, Is.EqualTo(4));
            s.Exit(1); s.Exit(2); s.Exit(4);
            Assert.That(s.ActiveZone, Is.EqualTo(4));           // doorway gap: hold the shot
        }

        [Test]
        public void Camera_selector_counts_cuts_only_on_change()
        {
            var s = new CameraZoneSelector();
            s.Enter(1, 0);
            s.Enter(2, 0);
            s.Exit(2);
            s.Enter(1, 0);
            Assert.That(s.CutCount, Is.EqualTo(3));
        }

        [Test]
        public void Camera_selector_does_not_double_cut_on_a_redundant_re_entry()
        {
            var s = new CameraZoneSelector();
            s.Enter(1, 1); // cut to zone 1
            s.Enter(2, 0); // lower priority: zone 1 stays active, no cut
            Assert.That(s.CutCount, Is.EqualTo(1));
            // A second overlapping collider on the already-active zone re-enters it.
            s.Enter(1, 1);
            Assert.That(s.CutCount, Is.EqualTo(1));
            Assert.That(s.ActiveZone, Is.EqualTo(1));
        }

        [Test]
        public void Movement_basis_holds_direction_across_a_cut_while_stick_held()
        {
            var b = new MovementBasis();
            b.Tick(0.016f, cameraYaw: 0f, stickHeld: true);
            b.Tick(0.016f, cameraYaw: (float)Math.PI, stickHeld: true); // camera cut to the reverse angle
            Assert.That(b.Yaw, Is.EqualTo(0f).Within(0.0005));                                  // grace: still the old basis
            for (var i = 0; i < 200; i++) b.Tick(0.016f, (float)Math.PI, true);
            Assert.That(Math.Abs(b.Yaw), Is.EqualTo((float)Math.PI).Within(0.005));
        }

        [Test]
        public void Movement_basis_adopts_new_camera_instantly_on_neutral_stick()
        {
            var b = new MovementBasis();
            b.Tick(0.016f, 0f, true);
            b.Tick(0.016f, 1.2f, stickHeld: false);
            Assert.That(b.Yaw, Is.EqualTo(1.2f).Within(5e-5));
        }

        [Test]
        public void Movement_basis_maps_stick_to_unity_world_axes()
        {
            var b = new MovementBasis();
            b.Tick(0.016f, 0f, false);
            var (fx, fz) = b.ToWorld(0f, 1f);
            Assert.That(fx, Is.EqualTo(0f).Within(5e-5)); Assert.That(fz, Is.EqualTo(1f).Within(5e-5));   // up = +Z
            var (rx, rz) = b.ToWorld(1f, 0f);
            Assert.That(rx, Is.EqualTo(1f).Within(5e-5)); Assert.That(rz, Is.EqualTo(0f).Within(5e-5));   // right = +X
        }
    }
}

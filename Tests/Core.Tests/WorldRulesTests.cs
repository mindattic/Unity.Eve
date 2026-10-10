using System;
using UnityEve.Core.Camera;
using UnityEve.Core.Items;
using UnityEve.Core.Util;
using Xunit;

namespace UnityEve.Core.Tests
{
    public class WorldRulesTests
    {
        private static Inventory Bag(int capacity) => new Inventory(capacity, id => id == "grenade" ? 3 : 9);

        [Fact]
        public void Inventory_stacks_then_spills_into_new_slots()
        {
            var bag = Bag(3);
            Assert.Equal(0, bag.Add("grenade", 5));
            Assert.Equal(2, bag.Slots.Count);
            Assert.Equal(5, bag.CountOf("grenade"));
        }

        [Fact]
        public void Inventory_reports_overflow_when_full()
        {
            var bag = Bag(1);
            Assert.Equal(2, bag.Add("grenade", 5));
            Assert.Equal(3, bag.CountOf("grenade"));
        }

        [Fact]
        public void Inventory_remove_frees_empty_slots()
        {
            var bag = Bag(4);
            bag.Add("medicine", 2);
            Assert.True(bag.TryRemove("medicine", 2));
            Assert.Empty(bag.Slots);
            Assert.False(bag.TryRemove("medicine"));
        }

        [Fact]
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
            Assert.Equal(a, b);
            Assert.True(a.Count <= 3);
        }

        [Fact]
        public void Camera_selector_prefers_priority_then_most_recent_and_holds_in_gaps()
        {
            var s = new CameraZoneSelector();
            s.Enter(1, priority: 0);
            Assert.Equal(1, s.ActiveZone);
            s.Enter(2, priority: 0);
            Assert.Equal(2, s.ActiveZone);           // most recent wins a tie
            s.Enter(3, priority: 5);
            s.Enter(4, priority: 0);
            Assert.Equal(3, s.ActiveZone);           // priority beats recency
            s.Exit(3);
            Assert.Equal(4, s.ActiveZone);
            s.Exit(1); s.Exit(2); s.Exit(4);
            Assert.Equal(4, s.ActiveZone);           // doorway gap: hold the shot
        }

        [Fact]
        public void Camera_selector_counts_cuts_only_on_change()
        {
            var s = new CameraZoneSelector();
            s.Enter(1, 0);
            s.Enter(2, 0);
            s.Exit(2);
            s.Enter(1, 0);
            Assert.Equal(3, s.CutCount);
        }

        [Fact]
        public void Movement_basis_holds_direction_across_a_cut_while_stick_held()
        {
            var b = new MovementBasis();
            b.Tick(0.016f, cameraYaw: 0f, stickHeld: true);
            b.Tick(0.016f, cameraYaw: (float)Math.PI, stickHeld: true); // camera cut to the reverse angle
            Assert.Equal(0f, b.Yaw, 3);                                  // grace: still the old basis
            for (var i = 0; i < 200; i++) b.Tick(0.016f, (float)Math.PI, true);
            Assert.Equal((float)Math.PI, Math.Abs(b.Yaw), 2);
        }

        [Fact]
        public void Movement_basis_adopts_new_camera_instantly_on_neutral_stick()
        {
            var b = new MovementBasis();
            b.Tick(0.016f, 0f, true);
            b.Tick(0.016f, 1.2f, stickHeld: false);
            Assert.Equal(1.2f, b.Yaw, 4);
        }

        [Fact]
        public void Movement_basis_maps_stick_to_unity_world_axes()
        {
            var b = new MovementBasis();
            b.Tick(0.016f, 0f, false);
            var (fx, fz) = b.ToWorld(0f, 1f);
            Assert.Equal(0f, fx, 4); Assert.Equal(1f, fz, 4);   // up = +Z
            var (rx, rz) = b.ToWorld(1f, 0f);
            Assert.Equal(1f, rx, 4); Assert.Equal(0f, rz, 4);   // right = +X
        }
    }
}

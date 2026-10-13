using System.Collections.Generic;
using UnityEve.Core.Battle;
using UnityEve.Core.Items;
using UnityEve.Core.Story;
using Xunit;

namespace UnityEve.Core.Tests
{
    public class StoryTests
    {
        private static Objectives Night() => new Objectives(new[]
        {
            new Objective("Search the Mercer house.", "item:key_shed"),
            new Objective("Take the shotgun from the shed.", "weapon:shotgun"),
            new Objective("Reach Saint Ansel's.", "flag:church_tried"),
            new Objective("Find the church key.", "item:key_church"),
            new Objective("Destroy what waits in the nave.", "cleared:Encounter_Boss_Nave"),
        });

        [Fact]
        public void Objectives_advance_as_facts_are_learned_and_announce_each_change()
        {
            var o = Night();
            var shown = new List<string>();
            o.Changed += x => shown.Add(x.Text);
            Assert.Equal("Search the Mercer house.", o.Current.Text);

            Assert.True(o.Learn("item:key_shed"));
            Assert.False(o.Learn("item:key_shed")); // facts are learned once
            Assert.Equal("Take the shotgun from the shed.", o.Current.Text);
            Assert.Equal(new[] { "Take the shotgun from the shed." }, shown);
        }

        [Fact]
        public void Objectives_can_be_completed_out_of_order_and_the_tracker_skips_ahead()
        {
            var o = Night();
            o.Learn("item:key_church");       // found the church key early
            Assert.Equal(0, o.Index);
            o.Learn("item:key_shed");
            o.Learn("weapon:shotgun");
            o.Learn("flag:church_tried");
            Assert.Equal("Destroy what waits in the nave.", o.Current.Text); // skipped the key step
        }

        [Fact]
        public void Completing_the_last_objective_ends_the_story_once()
        {
            var o = Night();
            var ended = 0;
            o.Completed += () => ended++;
            foreach (var f in new[] { "item:key_shed", "weapon:shotgun", "flag:church_tried", "item:key_church", "cleared:Encounter_Boss_Nave" })
                o.Learn(f);
            Assert.True(o.Done);
            Assert.Null(o.Current);
            Assert.Equal(1, ended);
        }

        [Fact]
        public void Restoring_facts_from_a_save_is_silent()
        {
            var o = Night();
            var changes = 0;
            o.Changed += _ => changes++;
            o.Restore(new[] { "item:key_shed", "weapon:shotgun" });
            Assert.Equal(0, changes);
            Assert.Equal(2, o.Index);
        }

        [Fact]
        public void A_save_needs_the_current_version_a_resume_point_and_a_living_heroine()
        {
            Assert.False(new SaveGame().IsValid);
            var s = new SaveGame { SavePoint = "SavePoint_BusStop", Hp = 40, Level = 3, Location = "Ashgrove Street", PlaySeconds = 845 };
            Assert.True(s.IsValid);
            Assert.Equal("LV 3  Ashgrove Street  0:14:05", s.Summary);
            s.Version = 0;
            Assert.False(s.IsValid);
        }

        [Fact]
        public void A_save_with_a_nulled_list_field_is_invalid()
        {
            var s = new SaveGame { SavePoint = "SavePoint_BusStop", Hp = 40, Level = 3, Location = "Ashgrove Street" };
            Assert.True(s.IsValid);
            s.Bag = null;
            Assert.False(s.IsValid);
        }

        [Fact]
        public void Restore_puts_progression_health_pe_and_the_bag_back_exactly()
        {
            var p = new Progression();
            p.Restore(4, 37, 2, 5);
            Assert.Equal((4, 37, 2, 5), (p.Level, p.Exp, p.BonusPoints, p.ActiveTime));

            var h = new Health(100);
            h.Restore(42);
            Assert.Equal(42, h.Current);
            h.Restore(500);
            Assert.Equal(100, h.Current);

            var pe = new ParasiteEnergy(80);
            pe.Restore(12.5f);
            Assert.Equal(12.5f, pe.Current);

            var bag = new Inventory(12, _ => 9);
            bag.Add("medicine", 2);
            bag.Clear();
            Assert.Empty(bag.Slots);
        }

        [Fact]
        public void Boss_calls_each_reinforcement_wave_once_as_its_health_falls()
        {
            var phases = new BossPhases(0.66f, 0.33f);
            Assert.Equal(0, phases.Check(0.9f));
            Assert.Equal(1, phases.Check(0.6f));
            Assert.Equal(0, phases.Check(0.5f));
            Assert.Equal(1, phases.Check(0.2f));
            Assert.Equal(0, phases.Check(0.1f));

            var burst = new BossPhases(0.66f, 0.33f);
            Assert.Equal(2, burst.Check(0.1f)); // one huge hit skipped past both thresholds
        }
    }
}

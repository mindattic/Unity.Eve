using System.Collections.Generic;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Items;
using Unity.Eve.Core.Story;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
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

        [Test]
        public void Objectives_advance_as_facts_are_learned_and_announce_each_change()
        {
            var o = Night();
            var shown = new List<string>();
            o.Changed += x => shown.Add(x.Text);
            Assert.That(o.Current.Text, Is.EqualTo("Search the Mercer house."));

            Assert.That(o.Learn("item:key_shed"), Is.True);
            Assert.That(o.Learn("item:key_shed"), Is.False); // facts are learned once
            Assert.That(o.Current.Text, Is.EqualTo("Take the shotgun from the shed."));
            Assert.That(shown, Is.EqualTo(new[] { "Take the shotgun from the shed." }));
        }

        [Test]
        public void Objectives_can_be_completed_out_of_order_and_the_tracker_skips_ahead()
        {
            var o = Night();
            o.Learn("item:key_church");       // found the church key early
            Assert.That(o.Index, Is.EqualTo(0));
            o.Learn("item:key_shed");
            o.Learn("weapon:shotgun");
            o.Learn("flag:church_tried");
            Assert.That(o.Current.Text, Is.EqualTo("Destroy what waits in the nave.")); // skipped the key step
        }

        [Test]
        public void Completing_the_last_objective_ends_the_story_once()
        {
            var o = Night();
            var ended = 0;
            o.Completed += () => ended++;
            foreach (var f in new[] { "item:key_shed", "weapon:shotgun", "flag:church_tried", "item:key_church", "cleared:Encounter_Boss_Nave" })
                o.Learn(f);
            Assert.That(o.Done, Is.True);
            Assert.That(o.Current, Is.Null);
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void Restoring_facts_from_a_save_is_silent()
        {
            var o = Night();
            var changes = 0;
            o.Changed += _ => changes++;
            o.Restore(new[] { "item:key_shed", "weapon:shotgun" });
            Assert.That(changes, Is.EqualTo(0));
            Assert.That(o.Index, Is.EqualTo(2));
        }

        [Test]
        public void A_save_needs_the_current_version_a_resume_point_and_a_living_heroine()
        {
            Assert.That(new SaveGame().IsValid, Is.False);
            var s = new SaveGame { SavePoint = "SavePoint_BusStop", Hp = 40, Level = 3, Location = "Ashgrove Street", PlaySeconds = 845 };
            Assert.That(s.IsValid, Is.True);
            Assert.That(s.Summary, Is.EqualTo("LV 3  Ashgrove Street  0:14:05"));
            s.Version = 0;
            Assert.That(s.IsValid, Is.False);
        }

        [Test]
        public void A_save_with_a_nulled_list_field_is_invalid()
        {
            var s = new SaveGame { SavePoint = "SavePoint_BusStop", Hp = 40, Level = 3, Location = "Ashgrove Street" };
            Assert.That(s.IsValid, Is.True);
            s.Bag = null;
            Assert.That(s.IsValid, Is.False);
        }

        [Test]
        public void Restore_puts_progression_health_pe_and_the_bag_back_exactly()
        {
            var p = new Progression();
            p.Restore(4, 37, 2, 5);
            Assert.That((p.Level, p.Exp, p.BonusPoints, p.ActiveTime), Is.EqualTo((4, 37, 2, 5)));

            var h = new Health(100);
            h.Restore(42);
            Assert.That(h.Current, Is.EqualTo(42));
            h.Restore(500);
            Assert.That(h.Current, Is.EqualTo(100));

            var pe = new ParasiteEnergy(80);
            pe.Restore(12.5f);
            Assert.That(pe.Current, Is.EqualTo(12.5f));

            var bag = new Inventory(12, _ => 9);
            bag.Add("medicine", 2);
            bag.Clear();
            Assert.That(bag.Slots, Is.Empty);
        }

        [Test]
        public void Boss_calls_each_reinforcement_wave_once_as_its_health_falls()
        {
            var phases = new BossPhases(0.66f, 0.33f);
            Assert.That(phases.Check(0.9f), Is.EqualTo(0));
            Assert.That(phases.Check(0.6f), Is.EqualTo(1));
            Assert.That(phases.Check(0.5f), Is.EqualTo(0));
            Assert.That(phases.Check(0.2f), Is.EqualTo(1));
            Assert.That(phases.Check(0.1f), Is.EqualTo(0));

            var burst = new BossPhases(0.66f, 0.33f);
            Assert.That(burst.Check(0.1f), Is.EqualTo(2)); // one huge hit skipped past both thresholds
        }
    }
}

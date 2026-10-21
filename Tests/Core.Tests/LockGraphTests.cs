using Unity.Eve.Core.Story;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    public class LockGraphTests
    {
        [Test]
        public void A_key_found_before_its_lock_makes_the_whole_graph_reachable()
        {
            var g = new LockGraph();
            g.Edge("Street", "Yard");                              // unlocked
            g.Edge("Yard", "Shed", "key_shed");                    // locked
            g.Key("Yard", "key_shed");                             // found on the way, not behind it

            var report = g.Validate("Street");

            Assert.That(report.Solvable, Is.True);
            Assert.That(report.KeysFound, Does.Contain("key_shed"));
        }

        [Test]
        public void A_key_locked_behind_its_own_gate_strands_that_region()
        {
            var g = new LockGraph();
            g.Edge("Street", "Vault", "key_vault");
            g.Key("Vault", "key_vault");                           // only ever found past the gate it opens

            var report = g.Validate("Street");

            Assert.That(report.Solvable, Is.False);
            Assert.That(report.UnreachableRegions, Is.EqualTo(new[] { "Vault" }));
        }

        [Test]
        public void A_key_behind_a_different_unsolvable_lock_strands_its_own_region_too()
        {
            var g = new LockGraph();
            g.Edge("Street", "Garage", "key_garage");              // nothing ever grants key_garage
            g.Key("Garage", "key_attic");                          // the attic key sits in the unreachable garage
            g.Edge("Street", "Attic", "key_attic");

            var report = g.Validate("Street");

            Assert.That(report.Solvable, Is.False);
            Assert.That(report.UnreachableRegions, Is.EqualTo(new[] { "Garage", "Attic" }));
        }

        [Test]
        public void One_key_can_legitimately_reopen_several_gates()
        {
            var g = new LockGraph();
            g.Edge("Street", "SideYard", "master");
            g.Edge("Street", "BackYard", "master");
            g.Key("Street", "master");

            var report = g.Validate("Street");

            Assert.That(report.Solvable, Is.True);
        }

        [Test]
        public void An_edge_declared_one_way_does_not_grant_reachability_backwards()
        {
            var g = new LockGraph();
            g.Edge("Cliff", "Beach", twoWay: false);               // a one-way drop down
            g.Region("Cliff");

            var report = g.Validate("Cliff");

            Assert.That(report.Solvable, Is.True); // Beach is reached; Cliff is the start so it's never "unreachable"
            var reverse = g.Validate("Beach");
            Assert.That(reverse.Solvable, Is.False); // from the bottom, Cliff is never reachable again
            Assert.That(reverse.UnreachableRegions, Is.EqualTo(new[] { "Cliff" }));
        }
    }
}

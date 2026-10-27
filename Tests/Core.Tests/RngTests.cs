using System.Collections.Generic;
using Unity.Eve.Core.Util;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>
    /// The seeded generator every battle replay depends on (U.EVE-LAW-1): same seed, same stream;
    /// every helper stays inside its documented range.
    /// </summary>
    public class RngTests
    {
        private const int Samples = 10000;

        [Test]
        public void Same_seed_replays_the_same_sequence()
        {
            var a = new Rng(1234);
            var b = new Rng(1234);
            for (var i = 0; i < 256; i++) Assert.That(b.NextUInt(), Is.EqualTo(a.NextUInt()), $"draw {i}");
        }

        [Test]
        public void Different_seeds_diverge()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            var same = 0;
            for (var i = 0; i < 64; i++)
                if (a.NextUInt() == b.NextUInt()) same++;
            Assert.That(same, Is.LessThan(2));
        }

        [Test]
        public void Seed_zero_still_yields_a_usable_stream()
        {
            var rng = new Rng(0);
            var seen = new HashSet<uint>();
            for (var i = 0; i < 100; i++) seen.Add(rng.NextUInt());
            Assert.That(seen.Count, Is.GreaterThan(95), "seed 0 must not collapse to a stuck or tiny cycle");
        }

        [Test]
        public void NextFloat_stays_in_zero_to_one_exclusive()
        {
            var rng = new Rng(7);
            for (var i = 0; i < Samples; i++)
            {
                var f = rng.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void Float_range_stays_in_min_to_max_exclusive()
        {
            var rng = new Rng(8);
            for (var i = 0; i < Samples; i++)
            {
                var f = rng.Range(-2.5f, 3.5f);
                Assert.That(f, Is.GreaterThanOrEqualTo(-2.5f).And.LessThan(3.5f));
            }
        }

        [Test]
        public void Int_range_stays_in_min_to_max_exclusive_and_reaches_both_ends()
        {
            var rng = new Rng(9);
            var seen = new HashSet<int>();
            for (var i = 0; i < Samples; i++)
            {
                var n = rng.Range(3, 8);
                Assert.That(n, Is.GreaterThanOrEqualTo(3).And.LessThan(8));
                seen.Add(n);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 3, 4, 5, 6, 7 }));
        }

        [TestCase(5, 5)]
        [TestCase(5, 4)]
        [TestCase(0, -10)]
        public void Int_range_returns_min_when_the_range_is_empty(int min, int maxExclusive)
        {
            var rng = new Rng(10);
            for (var i = 0; i < 20; i++) Assert.That(rng.Range(min, maxExclusive), Is.EqualTo(min));
        }

        [Test]
        public void Chance_zero_never_fires_and_chance_one_always_fires()
        {
            var rng = new Rng(11);
            for (var i = 0; i < Samples; i++)
            {
                Assert.That(rng.Chance(0f), Is.False);
                Assert.That(rng.Chance(1f), Is.True);
            }
        }
    }
}

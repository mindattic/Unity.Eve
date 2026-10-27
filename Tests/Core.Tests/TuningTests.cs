using System.IO;
using System.Linq;
using Unity.Eve.Core.Battle;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    /// <summary>Battle tuning is data (U.EVE-T39): the shipped file is complete, matches the built-in values, and a bad file changes nothing.</summary>
    public class TuningTests
    {
        [TearDown]
        public void TearDown() => BattleConfig.ResetToDefaults();

        private static string ShippedFile()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "repo root with Assets/ not found above the test directory");
            return File.ReadAllText(Path.Combine(dir.FullName, "Assets", "UnityEve", "Resources", "UnityEve", "battle-tuning.json"));
        }

        [Test]
        public void The_shipped_file_lists_every_knob_and_matches_the_built_in_values()
        {
            Assert.That(BattleConfig.Apply(ShippedFile()), Is.Empty);
            foreach (var knob in BattleConfig.Knobs)
                Assert.That(BattleConfig.Get(knob), Is.EqualTo(BattleConfig.Default(knob)).Within(1e-6), knob);
            foreach (var knob in BattleConfig.Knobs)
                Assert.That(ShippedFile(), Does.Contain($"\"{knob}\""), $"{knob} is missing from battle-tuning.json");
        }

        [Test]
        public void Every_tuning_number_is_a_knob()
        {
            Assert.That(BattleConfig.Knobs.Count, Is.GreaterThanOrEqualTo(79));
            Assert.That(BattleConfig.Knobs, Does.Contain(nameof(BattleConfig.AtbBaseRate)));
            Assert.That(BattleConfig.Knobs, Does.Contain(nameof(BattleConfig.MaxCommittedAttackers)));
        }

        [Test]
        public void A_file_changes_only_the_knobs_it_names()
        {
            Assert.That(BattleConfig.Apply("{ \"CriticalChance\": 0.2, \"BonusPointsPerLevel\": 5 }"), Is.Empty);
            Assert.That(BattleConfig.CriticalChance, Is.EqualTo(0.2f));
            Assert.That(BattleConfig.BonusPointsPerLevel, Is.EqualTo(5));
            Assert.That(BattleConfig.AtbBaseRate, Is.EqualTo((float)BattleConfig.Default(nameof(BattleConfig.AtbBaseRate))));
        }

        [TestCase("{ \"NoSuchKnob\": 1 }", "unknown knob")]
        [TestCase("{ \"CriticalChance\": -0.1 }", "negative")]
        [TestCase("{ \"CriticalChance\": 1.5 }", "chance")]
        [TestCase("{ \"CriticalChance\": \"high\" }", "not a number")]
        [TestCase("{ \"BonusPointsPerLevel\": 2.5 }", "whole number")]
        [TestCase("{ \"DamageVarianceLow\": 1.2, \"DamageVarianceHigh\": 1.1 }", "above")]
        [TestCase("{ \"MaxCommittedAttackers\": 0 }", "at least 1")]
        [TestCase("[1, 2]", "not a JSON object")]
        [TestCase("{ \"CriticalChance\": 0.2, \"CriticalChance\": 0.3 }", "twice")]
        public void A_bad_file_is_rejected_whole_with_a_reason(string json, string reason)
        {
            var before = BattleConfig.ToJson();
            var errors = BattleConfig.Apply("{ \"AtbBaseRate\": 60, " + json.TrimStart('{', ' '));
            if (json.StartsWith("[")) errors = BattleConfig.Apply(json);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(string.Join(" | ", errors), Does.Contain(reason));
            Assert.That(BattleConfig.ToJson(), Is.EqualTo(before), "nothing is applied when any value fails");
        }

        [Test]
        public void A_negative_flank_cosine_is_allowed()
        {
            Assert.That(BattleConfig.Apply("{ \"EnemyFlankOpeningFacing\": -0.3 }"), Is.Empty);
            Assert.That(BattleConfig.EnemyFlankOpeningFacing, Is.EqualTo(-0.3f));
        }

        [Test]
        public void ToJson_round_trips()
        {
            BattleConfig.Apply("{ \"PlayerRunSpeed\": 4.25 }");
            var json = BattleConfig.ToJson();
            BattleConfig.ResetToDefaults();
            Assert.That(BattleConfig.Apply(json), Is.Empty);
            Assert.That(BattleConfig.PlayerRunSpeed, Is.EqualTo(4.25f));
            Assert.That(BattleConfig.ToJson(), Is.EqualTo(json));
        }
    }
}

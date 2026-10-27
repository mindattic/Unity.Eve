using NUnit.Framework;
using Unity.Eve.Core.Battle;
using Unity.Eve.Game;

namespace Unity.Eve.Tests
{
    /// <summary>The shipped battle tuning loads from Resources and is accepted whole (U.EVE-T39).</summary>
    public class TuningLoaderTests
    {
        [TearDown]
        public void TearDown() => BattleConfig.ResetToDefaults();

        [Test]
        public void The_shipped_tuning_file_loads_and_is_accepted()
        {
            Assert.That(TuningLoader.Load(), Is.True);
            foreach (var knob in BattleConfig.Knobs)
                Assert.That(BattleConfig.Get(knob), Is.EqualTo(BattleConfig.Default(knob)).Within(1e-6), knob);
        }
    }
}

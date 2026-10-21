using Unity.Eve.Core.Battle;
using NUnit.Framework;

namespace Unity.Eve.Core.Tests
{
    public class BladeArtsTests
    {
        [Test]
        public void Slash_catches_the_front_arc_within_reach_and_nothing_behind()
        {
            var reach = BattleConfig.KatanaSlashReach;
            Assert.That(BladeArts.InSlashArc(0f, 1f, 0f, 1.5f, reach), Is.True);      // dead ahead
            Assert.That(BladeArts.InSlashArc(0f, 1f, 1f, 1f, reach), Is.True);        // 45 degrees off: inside the 110 degree arc
            Assert.That(BladeArts.InSlashArc(0f, 1f, 1.5f, 0f, reach), Is.False);     // 90 degrees off: beside, outside the arc
            Assert.That(BladeArts.InSlashArc(0f, 1f, 0f, -1f, reach), Is.False);      // behind
            Assert.That(BladeArts.InSlashArc(0f, 1f, 0f, reach + 0.1f, reach), Is.False); // out of reach
        }

        [Test]
        public void Rush_ends_past_the_target_by_the_overshoot()
        {
            var (x, z) = BladeArts.RushEnd(0f, 0f, 0f, 5f);
            Assert.That(x, Is.EqualTo(0f).Within(0.0005));
            Assert.That(z, Is.EqualTo(5f + BattleConfig.KatanaRushOvershoot).Within(0.0005));
        }

        [Test]
        public void Rush_cuts_everything_along_the_line_but_not_off_to_the_side()
        {
            var (ex, ez) = BladeArts.RushEnd(0f, 0f, 0f, 6f);
            Assert.That(BladeArts.OnRushPath(0f, 0f, ex, ez, 0f, 6f, 0.3f), Is.True);   // the target
            Assert.That(BladeArts.OnRushPath(0f, 0f, ex, ez, 0.5f, 3f, 0.3f), Is.True); // a creature in the way
            Assert.That(BladeArts.OnRushPath(0f, 0f, ex, ez, 0.2f, 7f, 0.3f), Is.True); // just past the target, inside the overshoot
            Assert.That(BladeArts.OnRushPath(0f, 0f, ex, ez, 2.5f, 3f, 0.3f), Is.False); // well off to the side
            Assert.That(BladeArts.OnRushPath(0f, 0f, ex, ez, 0f, -2f, 0.3f), Is.False);  // behind the start
        }

        [Test]
        public void Rush_trades_per_hit_power_for_reach_and_level_raises_both()
        {
            var slash = BladeArts.Power(BattleConfig.KatanaDamage, 1, rush: false);
            var rush = BladeArts.Power(BattleConfig.KatanaDamage, 1, rush: true);
            Assert.That(rush < slash, Is.True);
            Assert.That(BladeArts.Power(BattleConfig.KatanaDamage, 5, false) > slash, Is.True);
        }
    }
}

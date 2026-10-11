using UnityEve.Core.Battle;
using Xunit;

namespace UnityEve.Core.Tests
{
    public class BladeArtsTests
    {
        [Fact]
        public void Slash_catches_the_front_arc_within_reach_and_nothing_behind()
        {
            var reach = BattleConfig.KatanaSlashReach;
            Assert.True(BladeArts.InSlashArc(0f, 1f, 0f, 1.5f, reach));      // dead ahead
            Assert.True(BladeArts.InSlashArc(0f, 1f, 1f, 1f, reach));        // 45 degrees off: inside the 110 degree arc
            Assert.False(BladeArts.InSlashArc(0f, 1f, 1.5f, 0f, reach));     // 90 degrees off: beside, outside the arc
            Assert.False(BladeArts.InSlashArc(0f, 1f, 0f, -1f, reach));      // behind
            Assert.False(BladeArts.InSlashArc(0f, 1f, 0f, reach + 0.1f, reach)); // out of reach
        }

        [Fact]
        public void Rush_ends_past_the_target_by_the_overshoot()
        {
            var (x, z) = BladeArts.RushEnd(0f, 0f, 0f, 5f);
            Assert.Equal(0f, x, 3);
            Assert.Equal(5f + BattleConfig.KatanaRushOvershoot, z, 3);
        }

        [Fact]
        public void Rush_cuts_everything_along_the_line_but_not_off_to_the_side()
        {
            var (ex, ez) = BladeArts.RushEnd(0f, 0f, 0f, 6f);
            Assert.True(BladeArts.OnRushPath(0f, 0f, ex, ez, 0f, 6f, 0.3f));   // the target
            Assert.True(BladeArts.OnRushPath(0f, 0f, ex, ez, 0.5f, 3f, 0.3f)); // a creature in the way
            Assert.True(BladeArts.OnRushPath(0f, 0f, ex, ez, 0.2f, 7f, 0.3f)); // just past the target, inside the overshoot
            Assert.False(BladeArts.OnRushPath(0f, 0f, ex, ez, 2.5f, 3f, 0.3f)); // well off to the side
            Assert.False(BladeArts.OnRushPath(0f, 0f, ex, ez, 0f, -2f, 0.3f));  // behind the start
        }

        [Fact]
        public void Rush_trades_per_hit_power_for_reach_and_level_raises_both()
        {
            var slash = BladeArts.Power(BattleConfig.KatanaDamage, 1, rush: false);
            var rush = BladeArts.Power(BattleConfig.KatanaDamage, 1, rush: true);
            Assert.True(rush < slash);
            Assert.True(BladeArts.Power(BattleConfig.KatanaDamage, 5, false) > slash);
        }
    }
}

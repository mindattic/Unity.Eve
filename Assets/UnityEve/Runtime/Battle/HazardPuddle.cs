using UnityEngine;
using Unity.Eve.Core.Battle;

namespace Unity.Eve.Battle
{
    /// <summary>
    /// Acid/slime left where a ranged hit landed (<see cref="EnemyStats.LeavesPuddle"/>). The rule (how much,
    /// how wide, how long, when it burns) is the battle authority's; this only shows the glowing disc
    /// (<see cref="BattleFx.Puddle"/>) for as long as the authority keeps the puddle.
    /// </summary>
    public static class HazardPuddle
    {
        /// <returns>The disc, or null without effects in the scene.</returns>
        public static GameObject Show(Vector3 at)
        {
            var fx = BattleFx.Instance;
            return fx != null ? fx.Puddle(at, new Color(0.55f, 0.85f, 0.25f, 0.65f), BattleConfig.PuddleRadius) : null;
        }
    }
}

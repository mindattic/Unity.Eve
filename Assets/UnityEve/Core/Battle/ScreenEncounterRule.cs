using System.Collections.Generic;
using UnityEve.Core.Util;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// Random encounters on returning to a screen (a fixed-camera zone). A screen can roll for an
    /// encounter only when the player comes back to it after having been somewhere not adjacent to it,
    /// so stepping back and forth across a boundary never triggers one. Rolls also wait out a cooldown
    /// after any battle. Screens are opaque ids; the presentation supplies the adjacency.
    /// </summary>
    public sealed class ScreenEncounterRule
    {
        private readonly Dictionary<int, HashSet<int>> _adjacent = new Dictionary<int, HashSet<int>>();
        private readonly HashSet<int> _visited = new HashSet<int>();
        private readonly HashSet<int> _wentFar = new HashSet<int>();
        private readonly Rng _rng;
        private float _sinceBattle;
        private int _current = -1;

        /// <summary>Chance a returning visit rolls an encounter.</summary>
        public float Chance { get; set; } = BattleConfig.ScreenEncounterChance;
        /// <summary>Scaled seconds after any battle before another random encounter may start.</summary>
        public float Cooldown { get; set; } = BattleConfig.ScreenEncounterCooldownSeconds;

        public ScreenEncounterRule(Rng rng, float initialCooldownElapsed = 0f)
        {
            _rng = rng;
            _sinceBattle = initialCooldownElapsed;
        }

        /// <summary>Declares two screens neighbours (symmetric).</summary>
        public void Connect(int a, int b)
        {
            if (a == b) return;
            Neighbours(a).Add(b);
            Neighbours(b).Add(a);
        }

        public bool Adjacent(int a, int b) => a == b || (_adjacent.TryGetValue(a, out var n) && n.Contains(b));

        /// <param name="deltaSeconds">Scaled time: the cooldown does not run while the world is paused.</param>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds > 0f) _sinceBattle += deltaSeconds;
        }

        public void BattleEnded() => _sinceBattle = 0f;

        /// <summary>True when entering this screen now would be a qualifying return (before the roll).</summary>
        public bool IsReturn(int screen) => _visited.Contains(screen) && _wentFar.Contains(screen);

        /// <summary>The player entered a screen. True when an encounter should start here.</summary>
        /// <param name="canHost">False for screens with no encounter table (interiors): they still count as places visited.</param>
        public bool Enter(int screen, bool canHost = true)
        {
            if (screen == _current) return false;
            _current = screen;
            var qualifies = IsReturn(screen);

            // Everywhere not adjacent to here is now "far": a later return there qualifies.
            foreach (var s in _visited)
                if (!Adjacent(s, screen)) _wentFar.Add(s);
            _wentFar.Remove(screen);
            _visited.Add(screen);

            if (!qualifies || !canHost || _sinceBattle < Cooldown) return false;
            return _rng.Chance(Chance);
        }

        private HashSet<int> Neighbours(int s)
        {
            if (!_adjacent.TryGetValue(s, out var n)) _adjacent[s] = n = new HashSet<int>();
            return n;
        }
    }
}

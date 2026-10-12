using System.Collections.Generic;
using System.Linq;

namespace UnityEve.Core.Story
{
    /// <summary>
    /// The overworld's lock-and-key dependency graph: places the player can stand (regions), the doors
    /// and gates that connect them (edges, some needing a key), and where each key is found. Built by
    /// content generation as it places every door/gate/key, then validated once, so the game can never
    /// ship a lock with no reachable key behind it (the player trapping themselves with no way through).
    /// Pure rule, no Unity references — the same story a save file tells, just about the world instead
    /// of the player.
    /// </summary>
    public sealed class LockGraph
    {
        private readonly struct Connection
        {
            public readonly string From, To, Key;
            public readonly bool TwoWay;

            public Connection(string from, string to, string key, bool twoWay)
            {
                From = from;
                To = to;
                Key = key;
                TwoWay = twoWay;
            }
        }

        private readonly HashSet<string> _regions = new HashSet<string>();
        private readonly List<Connection> _edges = new List<Connection>();
        private readonly Dictionary<string, List<string>> _keysByRegion = new Dictionary<string, List<string>>();

        /// <summary>Declares a region exists even if nothing connects to it yet (e.g. the start).</summary>
        public void Region(string id) => _regions.Add(id);

        /// <summary>A door or gate between two regions; <paramref name="requiredKey"/> null means unlocked.</summary>
        public void Edge(string from, string to, string requiredKey = null, bool twoWay = true)
        {
            Region(from);
            Region(to);
            _edges.Add(new Connection(from, to, requiredKey, twoWay));
        }

        /// <summary>A key sitting somewhere the player can pick it up, once that region is reached.</summary>
        public void Key(string region, string keyId)
        {
            Region(region);
            if (!_keysByRegion.TryGetValue(region, out var list)) _keysByRegion[region] = list = new List<string>();
            list.Add(keyId);
        }

        /// <summary>
        /// Expands reachability from <paramref name="start"/>: a region is reachable once some reachable
        /// region connects to it by an unlocked edge, or a locked edge whose key was already found in a
        /// reachable region. Repeats to a fixpoint, since unlocking a region can reveal a key that opens
        /// another. Anything left unreached afterwards names exactly where the game would soft-lock.
        /// </summary>
        public LockGraphReport Validate(string start)
        {
            var reachable = new HashSet<string> { start };
            var keys = new HashSet<string>();
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var r in reachable.ToArray())
                    if (_keysByRegion.TryGetValue(r, out var ks))
                        foreach (var k in ks)
                            if (keys.Add(k)) changed = true;
                foreach (var e in _edges)
                {
                    if (e.Key != null && !keys.Contains(e.Key)) continue;
                    if (reachable.Contains(e.From) && reachable.Add(e.To)) changed = true;
                    if (e.TwoWay && reachable.Contains(e.To) && reachable.Add(e.From)) changed = true;
                }
            }
            var unreachable = _regions.Where(r => !reachable.Contains(r)).ToArray();
            return new LockGraphReport(unreachable, keys.ToArray());
        }
    }

    /// <summary>The result of <see cref="LockGraph.Validate"/>: empty <see cref="UnreachableRegions"/> means solvable.</summary>
    public readonly struct LockGraphReport
    {
        public readonly IReadOnlyList<string> UnreachableRegions;
        public readonly IReadOnlyList<string> KeysFound;

        public LockGraphReport(IReadOnlyList<string> unreachableRegions, IReadOnlyList<string> keysFound)
        {
            UnreachableRegions = unreachableRegions;
            KeysFound = keysFound;
        }

        public bool Solvable => UnreachableRegions.Count == 0;
    }
}

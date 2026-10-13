using System;
using System.Collections.Generic;
using UnityEve.Core.Util;

namespace UnityEve.Core.Items
{
    [Serializable]
    public sealed class LootEntry
    {
        public string ItemId = "";
        public int MinCount = 1;
        public int MaxCount = 1;
        /// <summary>Relative weight; an entry with an empty ItemId is a "nothing" roll.</summary>
        public float Weight = 1f;
    }

    /// <summary>What rummaging a container can produce. One weighted roll per pull.</summary>
    [Serializable]
    public sealed class LootTable
    {
        public List<LootEntry> Entries = new List<LootEntry>();
        public int Pulls = 1;

        /// <summary>One weighted pick; null for a "nothing" roll (an empty-id entry, or an empty table).</summary>
        public (string itemId, int count)? RollOne(Rng rng)
        {
            var total = 0f;
            foreach (var e in Entries) total += Math.Max(0f, e.Weight);
            if (total <= 0f) return null;
            var pick = rng.NextFloat() * total;
            foreach (var e in Entries)
            {
                var weight = Math.Max(0f, e.Weight);
                // A zero/negative-weight entry is unselectable, full stop — it must never match even
                // when the running pick lands exactly on the boundary before it (pick == 0f).
                if (weight <= 0f) continue;
                pick -= weight;
                if (pick > 0f) continue;
                return string.IsNullOrEmpty(e.ItemId) ? ((string, int)?)null : (e.ItemId, rng.Range(e.MinCount, e.MaxCount + 1));
            }
            return null;
        }

        public List<(string itemId, int count)> Roll(Rng rng)
        {
            var result = new List<(string, int)>();
            for (var p = 0; p < Pulls; p++)
            {
                var r = RollOne(rng);
                if (r.HasValue) result.Add(r.Value);
            }
            return result;
        }
    }
}

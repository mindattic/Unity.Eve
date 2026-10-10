using System;
using System.Collections.Generic;

namespace UnityEve.Core.Items
{
    public sealed class ItemSlot
    {
        public string ItemId { get; }
        public int Count { get; internal set; }

        public ItemSlot(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// A PE1-style bag: a limited number of slots, each holding one item type up to its stack size.
    /// Ammo lives in the bag too, so carrying bullets competes with carrying medicine.
    /// </summary>
    public sealed class Inventory
    {
        private readonly List<ItemSlot> _slots = new List<ItemSlot>();
        private readonly Func<string, int> _maxStack;

        public int Capacity { get; private set; }
        public IReadOnlyList<ItemSlot> Slots => _slots;
        public event Action Changed;

        /// <param name="capacity">Number of slots.</param>
        /// <param name="maxStack">Max stack per item id.</param>
        public Inventory(int capacity, Func<string, int> maxStack)
        {
            Capacity = capacity;
            _maxStack = maxStack;
        }

        public int CountOf(string itemId)
        {
            var total = 0;
            foreach (var s in _slots)
                if (s.ItemId == itemId) total += s.Count;
            return total;
        }

        /// <returns>How many could not fit (0 when everything was stored).</returns>
        public int Add(string itemId, int count)
        {
            if (count <= 0) return 0;
            var max = Math.Max(1, _maxStack(itemId));
            foreach (var s in _slots)
            {
                if (s.ItemId != itemId || s.Count >= max) continue;
                var take = Math.Min(max - s.Count, count);
                s.Count += take;
                count -= take;
                if (count == 0) break;
            }
            while (count > 0 && _slots.Count < Capacity)
            {
                var take = Math.Min(max, count);
                _slots.Add(new ItemSlot(itemId, take));
                count -= take;
            }
            Changed?.Invoke();
            return count;
        }

        public bool TryRemove(string itemId, int count = 1)
        {
            if (count <= 0 || CountOf(itemId) < count) return false;
            for (var i = _slots.Count - 1; i >= 0 && count > 0; i--)
            {
                var s = _slots[i];
                if (s.ItemId != itemId) continue;
                var take = Math.Min(s.Count, count);
                s.Count -= take;
                count -= take;
                if (s.Count == 0) _slots.RemoveAt(i);
            }
            Changed?.Invoke();
            return true;
        }

        public void SetCapacity(int capacity) => Capacity = Math.Max(capacity, _slots.Count);
    }
}

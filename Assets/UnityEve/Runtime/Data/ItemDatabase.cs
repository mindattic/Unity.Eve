using System.Collections.Generic;
using UnityEngine;

namespace UnityEve.Data
{
    /// <summary>Lookup from item id to definition, so Core (which only knows ids) can be presented.</summary>
    [CreateAssetMenu(menuName = "Unity Eve/Item Database", fileName = "ItemDatabase")]
    public sealed class ItemDatabase : ScriptableObject
    {
        public List<ItemDefinition> Items = new List<ItemDefinition>();

        private Dictionary<string, ItemDefinition> _byId;

        // Rebuilding on a count mismatch alone is wrong both ways: an in-place swap at the same
        // index never changes the count (stale lookup survives), and two entries sharing an id
        // collapse the dictionary below Items.Count, forcing a full rebuild on every single call.
        // OnValidate() covers edits made through the inspector/asset deserialization; Find() itself
        // just builds the cache once and keeps it.
        private void OnValidate() => _byId = null;

        public ItemDefinition Find(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, ItemDefinition>();
                foreach (var item in Items)
                    if (item != null) _byId[item.Id] = item;
            }
            return id != null && _byId.TryGetValue(id, out var def) ? def : null;
        }

        public int MaxStack(string id) => Find(id)?.Stats.MaxStack ?? 1;
        public string NameOf(string id) => Find(id)?.Stats.DisplayName ?? id;
    }
}

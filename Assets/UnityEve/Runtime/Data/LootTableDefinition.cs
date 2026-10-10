using UnityEngine;
using UnityEve.Core.Items;

namespace UnityEve.Data
{
    [CreateAssetMenu(menuName = "Unity Eve/Loot Table", fileName = "Loot")]
    public sealed class LootTableDefinition : ScriptableObject
    {
        public LootTable Table = new LootTable();
    }
}

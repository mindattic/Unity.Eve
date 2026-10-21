using UnityEngine;
using Unity.Eve.Core.Items;

namespace Unity.Eve.Data
{
    [CreateAssetMenu(menuName = "Unity Eve/Loot Table", fileName = "Loot")]
    public sealed class LootTableDefinition : ScriptableObject
    {
        public LootTable Table = new LootTable();
    }
}

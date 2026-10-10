using UnityEngine;
using UnityEve.Core.Battle;

namespace UnityEve.Data
{
    [CreateAssetMenu(menuName = "Unity Eve/Item", fileName = "Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public ItemStats Stats = new ItemStats();
        public string Id => Stats.Id;
    }
}

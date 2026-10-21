using UnityEngine;
using Unity.Eve.Core.Battle;

namespace Unity.Eve.Data
{
    /// <summary>A Parasite Energy ability.</summary>
    [CreateAssetMenu(menuName = "Unity Eve/PE Ability", fileName = "Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public AbilityStats Stats = new AbilityStats();
        public Color Tint = new Color(0.4f, 0.8f, 1f);
    }
}

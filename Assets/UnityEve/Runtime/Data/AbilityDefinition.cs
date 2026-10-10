using UnityEngine;
using UnityEve.Core.Battle;

namespace UnityEve.Data
{
    /// <summary>A Parasite Energy ability.</summary>
    [CreateAssetMenu(menuName = "Unity Eve/PE Ability", fileName = "Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public AbilityStats Stats = new AbilityStats();
        public Color Tint = new Color(0.4f, 0.8f, 1f);
    }
}

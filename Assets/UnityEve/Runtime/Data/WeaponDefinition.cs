using UnityEngine;
using Unity.Eve.Core.Battle;

namespace Unity.Eve.Data
{
    /// <summary>Designer-editable wrapper around a pure-Core <see cref="WeaponStats"/>.</summary>
    [CreateAssetMenu(menuName = "Unity Eve/Weapon", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public WeaponStats Stats = new WeaponStats();
        [Tooltip("Ammo item consumed on reload; empty for melee weapons.")]
        public ItemDefinition Ammo;
        public SoundId FireSound = SoundId.Gunshot;
    }
}

using UnityEngine;
using Unity.Eve.Data;
using Unity.Eve.Player;
using Unity.Eve.UI;

namespace Unity.Eve.Interaction
{
    /// <summary>A weapon to find in the world. Taking it adds it to the Equip list and readies it.</summary>
    public sealed class WeaponPickup : Interactable
    {
        [SerializeField] private WeaponDefinition _weapon;
        [SerializeField] private ItemDefinition _bonusAmmo;
        [SerializeField] private int _bonusAmmoCount = 2;

        public override string Prompt => _weapon != null ? $"Take {_weapon.Stats.DisplayName}" : null;

        public void Configure(WeaponDefinition weapon, ItemDefinition bonusAmmo, int bonusAmmoCount)
        {
            _weapon = weapon;
            _bonusAmmo = bonusAmmo;
            _bonusAmmoCount = bonusAmmoCount;
        }

        public override void Interact(PlayerCharacter player)
        {
            player.AddWeapon(_weapon);
            if (_bonusAmmo != null) player.Bag.Add(_bonusAmmo.Id, _bonusAmmoCount);
            AudioService.Play(SoundId.ItemFound);
            GameHud.Instance?.ShowMessage($"Got the {_weapon.Stats.DisplayName}! Equip it from the menu (Start) or in battle.", 4f);
            gameObject.SetActive(false);
        }
    }
}

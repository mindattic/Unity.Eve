using System;
using UnityEngine;
using UnityEve.Core.Battle;
using UnityEve.Core.Items;
using UnityEve.Data;

namespace UnityEve.Player
{
    /// <summary>
    /// The heroine's state: HP, Parasite Energy, level, bag and weapon. Thin MonoBehaviour shell
    /// over the pure Core types so the rules stay testable outside Unity.
    /// </summary>
    public sealed class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _items;
        [SerializeField] private WeaponDefinition _weapon;
        [SerializeField] private WeaponDefinition[] _weapons = Array.Empty<WeaponDefinition>();
        [SerializeField] private AbilityDefinition[] _abilities = Array.Empty<AbilityDefinition>();
        [SerializeField] private int _bagCapacity = 12;

        private readonly System.Collections.Generic.Dictionary<WeaponDefinition, int> _clips =
            new System.Collections.Generic.Dictionary<WeaponDefinition, int>();
        private float _hasteUntil;
        private float _barrierUntil;

        public Health Health { get; private set; }
        public ParasiteEnergy Pe { get; private set; }
        public Progression Progression { get; } = new Progression();
        public Inventory Bag { get; private set; }
        public ItemDatabase Items => _items;
        public WeaponDefinition Weapon => _weapon;
        public WeaponDefinition[] Weapons => _weapons;
        public AbilityDefinition[] Abilities => _abilities;
        public int RoundsInClip { get; private set; }
        public PlayerController Controller { get; private set; }

        public bool Hasted => Time.time < _hasteUntil;
        public bool Barrier => Time.time < _barrierUntil;
        public bool IsDead => Health.IsDead;

        /// <summary>Raised with the HP actually lost.</summary>
        public event Action<int> Damaged;
        public event Action<int> Healed;
        public event Action Died;

        public void Configure(ItemDatabase items, WeaponDefinition[] weapons, AbilityDefinition[] abilities)
        {
            _items = items;
            _weapons = weapons;
            _weapon = weapons.Length > 0 ? weapons[0] : null;
            _abilities = abilities;
        }

        private void Awake()
        {
            Controller = GetComponent<PlayerController>();
            Health = new Health(Progression.MaxHpForLevel);
            Pe = new ParasiteEnergy(Progression.MaxPeForLevel);
            Bag = new Inventory(_bagCapacity, id => _items != null ? _items.MaxStack(id) : 9);
            RoundsInClip = _weapon != null ? _weapon.Stats.ClipSize : 0;
        }

        public float AtbRate =>
            AtbGauge.Rate(Progression.ActiveTime, 0, _weapon != null ? _weapon.Stats.AtbMultiplier : 1f);

        public int TakeDamage(int amount)
        {
            if (Barrier) amount = Mathf.Max(1, amount / 2);
            var dealt = Health.Damage(amount);
            if (dealt > 0) Damaged?.Invoke(dealt);
            if (Health.IsDead) Died?.Invoke();
            return dealt;
        }

        public int Heal(int amount)
        {
            var healed = Health.Heal(amount);
            if (healed > 0) Healed?.Invoke(healed);
            return healed;
        }

        public void Revive()
        {
            Health.Revive();
            Pe.SetMax(Pe.Max, refill: true);
        }

        public void ApplyHaste(float seconds) => _hasteUntil = Time.time + seconds;
        public void ApplyBarrier(float seconds) => _barrierUntil = Time.time + seconds;

        public void ClearBuffs()
        {
            _hasteUntil = 0f;
            _barrierUntil = 0f;
        }

        /// <summary>Spends rounds from the clip; melee weapons never run dry.</summary>
        public void SpendRounds(int rounds)
        {
            if (_weapon == null || _weapon.Stats.Melee) return;
            RoundsInClip = Mathf.Max(0, RoundsInClip - rounds);
        }

        public bool NeedsReload => _weapon != null && !_weapon.Stats.Melee && RoundsInClip <= 0;
        public bool CanReload => _weapon != null && _weapon.Ammo != null && RoundsInClip < _weapon.Stats.ClipSize && Bag.CountOf(_weapon.Ammo.Id) > 0;

        /// <summary>Loads rounds from ammo items in the bag (each ammo item = one clip's worth up to Power rounds).</summary>
        public bool Reload()
        {
            if (!CanReload) return false;
            Bag.TryRemove(_weapon.Ammo.Id);
            RoundsInClip = _weapon.Stats.ClipSize;
            return true;
        }

        public void AddWeapon(WeaponDefinition weapon)
        {
            if (weapon == null || System.Array.IndexOf(_weapons, weapon) >= 0) return;
            var list = new System.Collections.Generic.List<WeaponDefinition>(_weapons) { weapon };
            _weapons = list.ToArray();
        }

        public void Equip(WeaponDefinition weapon)
        {
            if (weapon == null || weapon == _weapon) return;
            // Each gun keeps its own magazine, so swapping is never a free reload.
            if (_weapon != null) _clips[_weapon] = RoundsInClip;
            _weapon = weapon;
            RoundsInClip = weapon.Stats.Melee ? 0 : _clips.TryGetValue(weapon, out var left) ? left : weapon.Stats.ClipSize;
        }

        /// <returns>Levels gained.</returns>
        public int GainExp(int exp)
        {
            var levels = Progression.AddExp(exp);
            if (levels > 0)
            {
                // Demo: Bonus Points go straight into Active Time (faster ATB), as PE1 players usually did.
                Progression.TrySpendOnActiveTime(Progression.BonusPoints);
                Health.SetMax(Progression.MaxHpForLevel, refill: true);
                Pe.SetMax(Progression.MaxPeForLevel, refill: true);
            }
            return levels;
        }
    }
}

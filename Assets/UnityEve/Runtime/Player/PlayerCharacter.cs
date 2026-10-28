using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Items;
using Unity.Eve.Core.Session;
using Unity.Eve.Core.Sim;
using Unity.Eve.Data;

namespace Unity.Eve.Player
{
    /// <summary>
    /// The heroine as the scene knows her: her seat, and the definition assets (weapons, abilities, the item
    /// database) her rules are made from. Her state itself (HP, Parasite Energy, level, bag, magazines, buffs)
    /// is the Core <see cref="PlayerState"/> the authority owns; this only forwards to it and maps weapon
    /// indices back to their definitions.
    /// </summary>
    public sealed class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _items;
        [SerializeField] private WeaponDefinition _weapon;
        [SerializeField] private WeaponDefinition[] _weapons = Array.Empty<WeaponDefinition>();
        [SerializeField] private AbilityDefinition[] _abilities = Array.Empty<AbilityDefinition>();
        [SerializeField] private int _bagCapacity = 12;
        [Tooltip("The seat whose controls drive this character (0 = the first player).")]
        [SerializeField] private int _seat;

        private readonly List<WeaponDefinition> _weaponDefs = new List<WeaponDefinition>();
        private PlayerState _state;

        /// <summary>The player this character belongs to: its controls, and everything it owns, are keyed by this.</summary>
        public PlayerId Seat => PlayerId.Of(_seat);

        /// <summary>This character's seat's controls; null when no session is running.</summary>
        public PlayerControls Controls => Game.GameSession.ControlsOf(Seat);

        /// <summary>Her rules and state, owned by the authority (made on first use, so a sibling component's
        /// Awake can reach it whichever Awake runs first).</summary>
        public PlayerState State => _state ??= CreateState();

        public Health Health => State.Health;
        public ParasiteEnergy Pe => State.Pe;
        public Progression Progression => State.Progression;
        public Inventory Bag => State.Bag;
        public ItemDatabase Items => _items;
        /// <summary>The weapon in hand (its definition: name, sound, ammo item).</summary>
        public WeaponDefinition Weapon => State.WeaponIndex >= 0 && State.WeaponIndex < _weaponDefs.Count ? _weaponDefs[State.WeaponIndex] : null;

        /// <summary>The weapon definitions, index for index with <see cref="PlayerState.Weapons"/>.</summary>
        private List<WeaponDefinition> Defs
        {
            get
            {
                _ = State;
                return _weaponDefs;
            }
        }
        public WeaponDefinition[] Weapons => Defs.ToArray();
        public AbilityDefinition[] Abilities => _abilities;
        public int RoundsInClip => State.RoundsInClip;
        public PlayerController Controller { get; private set; }
        /// <summary>The katana she carries alongside whichever gun is equipped.</summary>
        public Katana Katana { get; private set; }

        public bool Hasted => State.Hasted;
        public bool Barrier => State.Barrier;
        public bool IsDead => State.IsDead;

        /// <summary>Raised with the HP actually lost.</summary>
        public event Action<int> Damaged
        {
            add => State.Damaged += value;
            remove => State.Damaged -= value;
        }

        public event Action<int> Healed
        {
            add => State.Healed += value;
            remove => State.Healed -= value;
        }

        public event Action Died
        {
            add => State.Died += value;
            remove => State.Died -= value;
        }

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
            Katana = GetComponent<Katana>();
            _ = State;
        }

        private PlayerState CreateState()
        {
            _weaponDefs.Clear();
            foreach (var w in _weapons ?? Array.Empty<WeaponDefinition>())
                if (w != null && !_weaponDefs.Contains(w)) _weaponDefs.Add(w);
            if (_weapon != null && !_weaponDefs.Contains(_weapon)) _weaponDefs.Insert(0, _weapon);
            return new PlayerState(Seat, _weaponDefs.Select(Slot), _weaponDefs.IndexOf(_weapon),
                (_abilities ?? Array.Empty<AbilityDefinition>()).Select(a => a.Stats).ToArray(),
                _bagCapacity, id => _items != null ? _items.MaxStack(id) : 9, id => _items != null ? _items.Find(id)?.Stats : null);
        }

        private static WeaponSlot Slot(WeaponDefinition w) => new WeaponSlot(w.Stats, w.Ammo != null ? w.Ammo.Id : null);

        public float AtbRate => State.AtbRate;

        public int TakeDamage(int amount) => State.TakeDamage(amount);
        public int Heal(int amount) => State.Heal(amount);
        public void Revive() => State.Revive();
        public void ApplyHaste(float seconds) => State.ApplyHaste(seconds);
        public void ApplyBarrier(float seconds) => State.ApplyBarrier(seconds);
        public void ClearBuffs() => State.ClearBuffs();

        /// <summary>Spends rounds from the clip; melee weapons never run dry.</summary>
        public void SpendRounds(int rounds) => State.SpendRounds(rounds);

        /// <summary>Loading a save: the magazine as it was.</summary>
        public void SetRounds(int rounds) => State.SetRounds(rounds);

        public bool NeedsReload => State.NeedsReload;
        public bool CanReload => State.CanReload;

        /// <summary>Loads a full magazine from one ammo item in the bag.</summary>
        public bool Reload() => State.Reload();

        public void AddWeapon(WeaponDefinition weapon)
        {
            if (weapon == null || Defs.Contains(weapon)) return;
            _weaponDefs.Add(weapon);
            State.AddWeapon(Slot(weapon));
        }

        /// <summary>The index of <paramref name="weapon"/> in what she carries, or -1.</summary>
        public int IndexOf(WeaponDefinition weapon) => Defs.IndexOf(weapon);

        public void Equip(WeaponDefinition weapon)
        {
            if (weapon == null || weapon == Weapon) return;
            State.Equip(Defs.IndexOf(weapon));
        }

        /// <returns>Levels gained.</returns>
        public int GainExp(int exp) => State.GainExp(exp);
    }
}

using System;
using System.Collections.Generic;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Items;
using Unity.Eve.Core.Session;

namespace Unity.Eve.Core.Sim
{
    /// <summary>A weapon a player carries: its rules and the bag item that reloads it.</summary>
    public sealed class WeaponSlot
    {
        public WeaponSlot(WeaponStats stats, string ammoId)
        {
            Stats = stats;
            AmmoId = ammoId;
        }

        public WeaponStats Stats { get; }
        /// <summary>The bag item one reload consumes; null for a weapon that never reloads.</summary>
        public string AmmoId { get; }
    }

    /// <summary>
    /// Everything a player owns that the rules read or change: HP, Parasite Energy, level, bag, weapons and
    /// magazines, abilities and timed buffs. Pure data and rules, keyed by seat; <c>PlayerCharacter</c> is
    /// only its presentation shell. Buff timers run on the authority's <see cref="SimClock"/>.
    /// </summary>
    public sealed class PlayerState
    {
        private readonly List<WeaponSlot> _weapons = new List<WeaponSlot>();
        private readonly Dictionary<int, int> _clips = new Dictionary<int, int>();
        private readonly Func<string, ItemStats> _items;
        private SimClock _clock;
        private float _hasteUntil;
        private float _barrierUntil;

        /// <param name="items">Looks up an item's rules by id (null when unknown).</param>
        /// <param name="maxStack">The bag's stack limit per item id.</param>
        public PlayerState(PlayerId seat, IEnumerable<WeaponSlot> weapons, int equipped, IReadOnlyList<AbilityStats> abilities,
            int bagCapacity, Func<string, int> maxStack, Func<string, ItemStats> items)
        {
            Seat = seat;
            _items = items ?? (_ => null);
            if (weapons != null) _weapons.AddRange(weapons);
            WeaponIndex = equipped >= 0 && equipped < _weapons.Count ? equipped : _weapons.Count > 0 ? 0 : -1;
            Abilities = abilities ?? Array.Empty<AbilityStats>();
            Health = new Health(Progression.MaxHpForLevel);
            Pe = new ParasiteEnergy(Progression.MaxPeForLevel);
            Bag = new Inventory(bagCapacity, maxStack ?? (_ => 9));
            RoundsInClip = Weapon != null ? Weapon.ClipSize : 0;
        }

        public PlayerId Seat { get; }
        public Health Health { get; }
        public ParasiteEnergy Pe { get; }
        public Progression Progression { get; } = new Progression();
        public Inventory Bag { get; }
        public IReadOnlyList<AbilityStats> Abilities { get; }
        public IReadOnlyList<WeaponSlot> Weapons => _weapons;
        /// <summary>Index into <see cref="Weapons"/> of the weapon in hand; -1 with none.</summary>
        public int WeaponIndex { get; private set; }
        public WeaponSlot Slot => WeaponIndex >= 0 ? _weapons[WeaponIndex] : null;
        public WeaponStats Weapon => Slot?.Stats;
        public int RoundsInClip { get; private set; }

        /// <summary>Scaled seconds now (0 until the authority's clock is attached).</summary>
        public float Now => _clock != null ? _clock.Now : 0f;
        public bool Hasted => Now < _hasteUntil;
        public bool Barrier => Now < _barrierUntil;
        public bool IsDead => Health.IsDead;

        /// <summary>Raised with the HP actually lost.</summary>
        public event Action<int> Damaged;
        public event Action<int> Healed;
        public event Action Died;

        /// <summary>Times buffs on the authority's clock.</summary>
        public void UseClock(SimClock clock) => _clock = clock;

        public ItemStats FindItem(string id) => id == null ? null : _items(id);

        public float AtbRate => AtbGauge.Rate(Progression.ActiveTime, 0, Weapon != null ? Weapon.AtbMultiplier : 1f);

        public int TakeDamage(int amount)
        {
            if (Barrier) amount = Math.Max(1, amount / 2);
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

        public void ApplyHaste(float seconds) => _hasteUntil = Now + seconds;
        public void ApplyBarrier(float seconds) => _barrierUntil = Now + seconds;

        public void ClearBuffs()
        {
            _hasteUntil = 0f;
            _barrierUntil = 0f;
        }

        /// <summary>Spends rounds from the clip; melee weapons never run dry.</summary>
        public void SpendRounds(int rounds)
        {
            if (Weapon == null || Weapon.Melee) return;
            RoundsInClip = Math.Max(0, RoundsInClip - rounds);
        }

        /// <summary>Loading a save: the magazine as it was.</summary>
        public void SetRounds(int rounds) =>
            RoundsInClip = Weapon == null || Weapon.Melee ? 0 : rounds < 0 ? 0 : rounds > Weapon.ClipSize ? Weapon.ClipSize : rounds;

        public bool NeedsReload => Weapon != null && !Weapon.Melee && RoundsInClip <= 0;
        public bool CanReload => Weapon != null && Slot.AmmoId != null && RoundsInClip < Weapon.ClipSize && Bag.CountOf(Slot.AmmoId) > 0;

        /// <summary>Loads a full magazine from one ammo item in the bag.</summary>
        public bool Reload()
        {
            if (!CanReload) return false;
            Bag.TryRemove(Slot.AmmoId);
            RoundsInClip = Weapon.ClipSize;
            return true;
        }

        /// <returns>The new weapon's index.</returns>
        public int AddWeapon(WeaponSlot weapon)
        {
            _weapons.Add(weapon);
            if (WeaponIndex < 0) WeaponIndex = _weapons.Count - 1;
            return _weapons.Count - 1;
        }

        /// <summary>Draws weapon <paramref name="index"/>. Each gun keeps its own magazine, so swapping is never a free reload.</summary>
        public bool Equip(int index)
        {
            if (index < 0 || index >= _weapons.Count || index == WeaponIndex) return false;
            if (WeaponIndex >= 0) _clips[WeaponIndex] = RoundsInClip;
            WeaponIndex = index;
            var w = _weapons[index].Stats;
            RoundsInClip = w.Melee ? 0 : _clips.TryGetValue(index, out var left) ? left : w.ClipSize;
            return true;
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

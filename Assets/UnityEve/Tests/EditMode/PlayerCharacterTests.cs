using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Eve.Core.Battle;
using Unity.Eve.Data;
using Unity.Eve.Player;

namespace Unity.Eve.Tests
{
    /// <summary>PlayerCharacter's weapon, ammo and damage rules: the thin shell between Core and the battle.</summary>
    public class PlayerCharacterTests
    {
        private GameObject _go;
        private ItemDatabase _items;
        private ItemDefinition _pistolAmmo;
        private ItemDefinition _shells;
        private WeaponDefinition _pistol;
        private WeaponDefinition _shotgun;
        private WeaponDefinition _katana;

        private static T So<T>() where T : ScriptableObject => ScriptableObject.CreateInstance<T>();

        private static ItemDefinition Ammo(string id)
        {
            var def = So<ItemDefinition>();
            def.Stats = new ItemStats { Id = id, DisplayName = id, Effect = ItemEffect.Ammo, MaxStack = 9 };
            return def;
        }

        private static WeaponDefinition Weapon(string id, int clip, ItemDefinition ammo, bool melee = false)
        {
            var def = So<WeaponDefinition>();
            def.Stats = new WeaponStats { Id = id, DisplayName = id, ClipSize = clip, Melee = melee };
            def.Ammo = ammo;
            return def;
        }

        [SetUp]
        public void SetUp()
        {
            _pistolAmmo = Ammo("pistol_ammo");
            _shells = Ammo("shells");
            _items = So<ItemDatabase>();
            _items.Items.Add(_pistolAmmo);
            _items.Items.Add(_shells);
            _pistol = Weapon("pistol", 10, _pistolAmmo);
            _shotgun = Weapon("shotgun", 4, _shells);
            _katana = Weapon("katana", 0, null, melee: true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (var o in new Object[] { _items, _pistolAmmo, _shells, _pistol, _shotgun, _katana })
                if (o != null) Object.DestroyImmediate(o);
        }

        /// <summary>Configure, then run Awake the way entering Play would (edit mode never calls it).</summary>
        private PlayerCharacter Kat(params WeaponDefinition[] weapons)
        {
            _go = new GameObject("Kat");
            var kat = _go.AddComponent<PlayerCharacter>();
            kat.Configure(_items, weapons, new AbilityDefinition[0]);
            typeof(PlayerCharacter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(kat, null);
            return kat;
        }

        [Test]
        public void Each_gun_keeps_its_own_magazine_so_swapping_is_never_a_free_reload()
        {
            var kat = Kat(_pistol, _shotgun);
            kat.SpendRounds(7);
            Assert.That(kat.RoundsInClip, Is.EqualTo(3));
            kat.Equip(_shotgun);
            Assert.That(kat.RoundsInClip, Is.EqualTo(4), "a gun never drawn before starts full");
            kat.SpendRounds(1);
            kat.Equip(_pistol);
            Assert.That(kat.RoundsInClip, Is.EqualTo(3));
            kat.Equip(_shotgun);
            Assert.That(kat.RoundsInClip, Is.EqualTo(3));
        }

        [Test]
        public void Melee_weapons_report_zero_rounds_and_never_spend()
        {
            var kat = Kat(_katana);
            Assert.That(kat.RoundsInClip, Is.EqualTo(0));
            kat.SpendRounds(5);
            Assert.That(kat.RoundsInClip, Is.EqualTo(0));
            Assert.That(kat.NeedsReload, Is.False);
            kat.SetRounds(8);
            Assert.That(kat.RoundsInClip, Is.EqualTo(0));
        }

        [TestCase(-4, 0)]
        [TestCase(6, 6)]
        [TestCase(99, 10)]
        public void SetRounds_clamps_to_the_clip(int rounds, int expected)
        {
            var kat = Kat(_pistol);
            kat.SetRounds(rounds);
            Assert.That(kat.RoundsInClip, Is.EqualTo(expected));
        }

        [Test]
        public void Reload_needs_ammo_in_the_bag_and_consumes_exactly_one_item()
        {
            var kat = Kat(_pistol);
            kat.SpendRounds(10);
            Assert.That(kat.NeedsReload, Is.True);
            Assert.That(kat.CanReload, Is.False);
            Assert.That(kat.Reload(), Is.False);

            kat.Bag.Add("pistol_ammo", 2);
            Assert.That(kat.CanReload, Is.True);
            Assert.That(kat.Reload(), Is.True);
            Assert.That(kat.RoundsInClip, Is.EqualTo(10));
            Assert.That(kat.Bag.CountOf("pistol_ammo"), Is.EqualTo(1));
            Assert.That(kat.CanReload, Is.False, "a full clip never reloads");
        }

        [Test]
        public void AddWeapon_ignores_null_and_duplicates()
        {
            var kat = Kat(_pistol);
            kat.AddWeapon(null);
            kat.AddWeapon(_pistol);
            Assert.That(kat.Weapons.Length, Is.EqualTo(1));
            kat.AddWeapon(_shotgun);
            Assert.That(kat.Weapons, Is.EqualTo(new[] { _pistol, _shotgun }));
        }

        [Test]
        public void Barrier_halves_damage_with_a_floor_of_one()
        {
            var kat = Kat(_pistol);
            kat.ApplyBarrier(1000f);
            Assert.That(kat.TakeDamage(10), Is.EqualTo(5));
            Assert.That(kat.TakeDamage(1), Is.EqualTo(1));
            kat.ClearBuffs();
            Assert.That(kat.TakeDamage(10), Is.EqualTo(10));
        }

        [Test]
        public void Reaching_zero_hp_raises_Died()
        {
            var kat = Kat(_pistol);
            var died = 0;
            var lost = 0;
            kat.Died += () => died++;
            kat.Damaged += n => lost += n;
            kat.TakeDamage(kat.Health.Max + 50);
            Assert.That(kat.IsDead, Is.True);
            Assert.That(died, Is.EqualTo(1));
            Assert.That(lost, Is.EqualTo(kat.Health.Max));
        }

        [Test]
        public void Level_up_auto_spends_bonus_points_and_refills_hp_and_pe()
        {
            var kat = Kat(_pistol);
            kat.TakeDamage(30);
            kat.Pe.TrySpend(20f);
            var levels = kat.GainExp(Progression.ExpForLevel(1));
            Assert.That(levels, Is.EqualTo(1));
            Assert.That(kat.Progression.BonusPoints, Is.EqualTo(0));
            Assert.That(kat.Progression.ActiveTime, Is.EqualTo(BattleConfig.BonusPointsPerLevel * 2));
            Assert.That(kat.Health.Max, Is.EqualTo(kat.Progression.MaxHpForLevel));
            Assert.That(kat.Health.Current, Is.EqualTo(kat.Health.Max));
            Assert.That(kat.Pe.Current, Is.EqualTo(kat.Pe.Max));
        }
    }
}

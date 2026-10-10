using System;

namespace UnityEve.Core.Battle
{
    /// <summary>A firearm or melee weapon. PE1 weapons differ by damage, range dome, ATB multiplier and rate of fire.</summary>
    [Serializable]
    public sealed class WeaponStats
    {
        public string Id = "pistol";
        public string DisplayName = "Pistol";
        public int Damage = 12;
        /// <summary>Radius of the range dome in metres; only enemies inside it can be targeted.</summary>
        public float Range = 7f;
        /// <summary>PE1 gun multiplier on ATB fill (club 2.0, pistol 1.0, shotgun 0.8, rocket 0.4).</summary>
        public float AtbMultiplier = 1f;
        public int ClipSize = 10;
        /// <summary>Rate of fire: rounds per turn. Each round deals a share of the turn's damage (PE1 table).</summary>
        public int RoundsPerTurn = 1;
        /// <summary>Burst: the turn's rounds spread across every target in the dome instead of one.</summary>
        public bool Burst;
        /// <summary>Seconds the player is rooted while the turn plays out in real time.</summary>
        public float RootSeconds = 0.45f;
        /// <summary>Melee weapons never use ammo.</summary>
        public bool Melee;
    }

    public enum ItemEffect
    {
        None,
        RestoreHp,
        RestorePe,
        /// <summary>Thrown: damages every enemy within <see cref="ItemStats.Radius"/> of the impact.</summary>
        Explosive,
        /// <summary>Thrown: stuns every enemy within the radius.</summary>
        Flash,
        Ammo,
        Key,
    }

    [Serializable]
    public sealed class ItemStats
    {
        public string Id = "medicine";
        public string DisplayName = "Medicine";
        public string Description = "";
        public ItemEffect Effect = ItemEffect.RestoreHp;
        public int Power = 50;
        public float Radius;
        public float StunSeconds;
        public int MaxStack = 9;
        /// <summary>True when the item can be used from the battle Item menu.</summary>
        public bool UsableInBattle = true;
    }

    public enum AbilityEffect
    {
        Heal,
        /// <summary>A bolt of mitochondrial energy at one target in range.</summary>
        EnergyShot,
        /// <summary>Doubles ATB fill for a duration.</summary>
        Haste,
        /// <summary>Halves enemy speed and attack tempo for a duration.</summary>
        Slow,
        /// <summary>Halves incoming damage for a duration.</summary>
        Barrier,
    }

    [Serializable]
    public sealed class AbilityStats
    {
        public string Id = "heal";
        public string DisplayName = "Heal";
        public string Description = "";
        public AbilityEffect Effect = AbilityEffect.Heal;
        public float PeCost = 30f;
        public int Power = 60;
        public float DurationSeconds;
        public float Range = 8f;
        public bool NeedsTarget => Effect == AbilityEffect.EnergyShot;
    }

    /// <summary>A hittable region of an enemy (PE-style part damage via ExperimentEve).</summary>
    [Serializable]
    public sealed class BodyPartStats
    {
        public string Name = "body";
        public float DamageMultiplier = 1f;
        public bool WeakPoint;
    }

    [Serializable]
    public sealed class EnemyStats
    {
        public string Id = "rat";
        public string DisplayName = "Rat";
        public int Level = 1;
        public int MaxHp = 30;
        public int Defense;
        public int AttackDamage = 6;
        public float MoveSpeed = 2.5f;
        public float Reach = 1f;
        public float WindupSeconds = 0.45f;
        public float RecoverSeconds = 0.45f;
        public float CooldownSeconds = 1f;
        public int Exp = 10;
        public float HoverHeight;
    }
}

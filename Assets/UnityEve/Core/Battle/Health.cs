using System;

namespace Unity.Eve.Core.Battle
{
    /// <summary>Hit points with clamped damage and healing.</summary>
    public sealed class Health
    {
        public int Max { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public float Fraction => Max <= 0 ? 0f : (float)Current / Max;

        public Health(int max)
        {
            Max = Math.Max(1, max);
            Current = Max;
        }

        /// <returns>Damage actually dealt.</returns>
        public int Damage(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            var dealt = Math.Min(Current, amount);
            Current -= dealt;
            return dealt;
        }

        /// <returns>HP actually restored.</returns>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            var healed = Math.Min(Max - Current, amount);
            Current += healed;
            return healed;
        }

        public void SetMax(int max, bool refill)
        {
            Max = Math.Max(1, max);
            Current = refill ? Max : Math.Min(Current, Max);
        }

        public void Revive() => Current = Max;

        /// <summary>Loading a save: exact current HP, clamped to the maximum.</summary>
        public void Restore(int current) => Current = current < 1 ? 1 : current > Max ? Max : current;
    }
}

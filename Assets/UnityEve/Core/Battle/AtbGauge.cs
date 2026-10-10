using System;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// The Active Time Bar. It fills in real time while the player moves; when full, the player may
    /// open the command menu (which pauses the world). Spending a turn empties it.
    /// </summary>
    public sealed class AtbGauge
    {
        /// <summary>Normalized fill, 0..1.</summary>
        public float Value { get; private set; }

        public bool IsFull => Value >= 1f;

        /// <summary>Raised once each time the gauge reaches full.</summary>
        public event Action Filled;

        /// <summary>PE1 rate: (50 + activeTime - atDownPenalty*2) × weapon multiplier, floored.</summary>
        public static float Rate(int activeTime, int atDownPenalty, float weaponMultiplier)
        {
            var rate = (BattleConfig.AtbBaseRate + activeTime - atDownPenalty * 2f) * weaponMultiplier;
            return Math.Max(BattleConfig.AtbMinimumRate, rate);
        }

        /// <summary>Converts a PE1 rate into gauge fill per second.</summary>
        public static float FillPerSecond(float rate) =>
            rate / BattleConfig.AtbBaseRate / BattleConfig.BaseAtbSeconds;

        public void Tick(float deltaSeconds, float rate, bool hasted = false)
        {
            if (IsFull || deltaSeconds <= 0f) return;
            var perSecond = FillPerSecond(rate) * (hasted ? BattleConfig.HasteMultiplier : 1f);
            Value = Math.Min(1f, Value + perSecond * deltaSeconds);
            if (IsFull) Filled?.Invoke();
        }

        public void Spend() => Value = 0f;

        public void Fill()
        {
            if (IsFull) return;
            Value = 1f;
            Filled?.Invoke();
        }
    }
}

using System;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// Parasite Energy: the pool PE abilities draw from. It regenerates in real time during battle,
    /// and — as in PE1 — every cast within one fight slows that regeneration.
    /// </summary>
    public sealed class ParasiteEnergy
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public int CastsThisBattle { get; private set; }

        public ParasiteEnergy(float max)
        {
            Max = max;
            Current = max;
        }

        public float RegenMultiplier =>
            Math.Max(BattleConfig.PeRegenFloor, 1f - CastsThisBattle * BattleConfig.PeRegenFalloffPerCast);

        public bool CanAfford(float cost) => Current >= cost;

        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost)) return false;
            Current -= cost;
            CastsThisBattle++;
            return true;
        }

        public void Regenerate(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            Current = Math.Min(Max, Current + BattleConfig.PeRegenPerSecond * RegenMultiplier * deltaSeconds);
        }

        public void BeginBattle() => CastsThisBattle = 0;

        /// <summary>Loading a save: exact current PE, clamped to the maximum.</summary>
        public void Restore(float current) => Current = current < 0f ? 0f : current > Max ? Max : current;

        public void SetMax(float max, bool refill)
        {
            Max = max;
            Current = refill ? max : Math.Min(Current, max);
        }
    }
}

using System;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// Level, experience and Bonus Points. PE1 grants BP on level-up to spend on Active Time and
    /// equipment; here BP raise Active Time (faster ATB) automatically for the demo.
    /// </summary>
    public sealed class Progression
    {
        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int BonusPoints { get; private set; }
        public int ActiveTime { get; private set; }

        public int ExpToNext => ExpForLevel(Level);

        /// <summary>Loading a save: put the counters back exactly as they were.</summary>
        public void Restore(int level, int exp, int bonusPoints, int activeTime)
        {
            Level = level < 1 ? 1 : level;
            Exp = exp < 0 ? 0 : exp;
            BonusPoints = bonusPoints < 0 ? 0 : bonusPoints;
            ActiveTime = activeTime < 0 ? 0 : activeTime;
        }

        public static int ExpForLevel(int level) =>
            (int)Math.Round(BattleConfig.ExpPerLevelBase * Math.Pow(BattleConfig.ExpCurveGrowth, level - 1));

        /// <returns>Number of levels gained.</returns>
        public int AddExp(int amount)
        {
            if (amount <= 0) return 0;
            Exp += amount;
            var gained = 0;
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                BonusPoints += BattleConfig.BonusPointsPerLevel;
                gained++;
            }
            return gained;
        }

        public bool TrySpendOnActiveTime(int points)
        {
            if (points <= 0 || points > BonusPoints) return false;
            BonusPoints -= points;
            ActiveTime += points * 2;
            return true;
        }

        public int MaxHpForLevel => 80 + (Level - 1) * 14;
        public float MaxPeForLevel => 60 + (Level - 1) * 10;
    }

    public static class EscapeRule
    {
        /// <summary>Escape succeeds more often against weaker packs; it never fully guarantees or forbids.</summary>
        public static float Chance(int playerLevel, int strongestEnemyLevel)
        {
            var c = BattleConfig.EscapeBaseChance + (playerLevel - strongestEnemyLevel) * BattleConfig.EscapeChancePerLevel;
            return Math.Min(BattleConfig.EscapeMaxChance, Math.Max(BattleConfig.EscapeMinChance, c));
        }
    }
}

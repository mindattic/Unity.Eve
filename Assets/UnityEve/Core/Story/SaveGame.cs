using System;
using System.Collections.Generic;

namespace UnityEve.Core.Story
{
    [Serializable]
    public sealed class SavedSlot
    {
        public string ItemId;
        public int Count;
    }

    /// <summary>
    /// Everything a save point writes. Plain public fields so any serializer (the game uses Unity's
    /// JsonUtility) round-trips it. World objects are named by their stable scene names.
    /// </summary>
    [Serializable]
    public sealed class SaveGame
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string SavePoint;
        public string Location;
        public string SavedAt;
        public float PlaySeconds;
        public int Saves;
        public int Kills;

        public int Level = 1;
        public int Exp;
        public int BonusPoints;
        public int ActiveTime;
        public int Hp;
        public float Pe;

        public List<SavedSlot> Bag = new List<SavedSlot>();
        public List<string> Weapons = new List<string>();
        public string Equipped;
        public int RoundsInClip;

        public List<string> Facts = new List<string>();
        public List<string> ClearedEncounters = new List<string>();
        public List<string> SearchedContainers = new List<string>();
        public List<string> TakenPickups = new List<string>();
        public List<string> UnlockedDoors = new List<string>();
        public List<string> UnlockedGates = new List<string>();

        /// <summary>A save is usable when it is the current version and names where to resume.</summary>
        public bool IsValid => Version == CurrentVersion && !string.IsNullOrEmpty(SavePoint) && Level >= 1 && Hp > 0;

        /// <summary>"LV 3  Kingsport Foods  0:14:05" for the title screen's Continue entry.</summary>
        public string Summary
        {
            get
            {
                var t = TimeSpan.FromSeconds(PlaySeconds);
                return $"LV {Level}  {Location}  {(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
            }
        }
    }

    /// <summary>
    /// The boss's reinforcement waves: crossing each health threshold (going down) calls one wave,
    /// once. Pure rule; presentation spawns the creatures.
    /// </summary>
    public sealed class BossPhases
    {
        private readonly float[] _thresholds;
        private int _next;

        public BossPhases(params float[] thresholds)
        {
            _thresholds = thresholds;
            Array.Sort(_thresholds);
            Array.Reverse(_thresholds);
        }

        public int Phase => _next;

        /// <returns>How many waves to call now (usually 0 or 1; more if a big hit skipped past several).</returns>
        public int Check(float healthFraction)
        {
            var waves = 0;
            while (_next < _thresholds.Length && healthFraction <= _thresholds[_next])
            {
                _next++;
                waves++;
            }
            return waves;
        }
    }
}

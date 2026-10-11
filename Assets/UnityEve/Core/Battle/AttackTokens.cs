using System.Collections.Generic;
using UnityEve.Core.Util;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// The pack's attack tokens (the classic action-game pattern): only a creature holding a token may
    /// engage the player. The rest keep their distance. After a token comes back there is a short,
    /// seeded breather before the next creature may take it, so the pack takes turns instead of mobbing.
    /// </summary>
    public sealed class AttackTokens
    {
        private readonly HashSet<object> _holders = new HashSet<object>();
        private readonly Rng _rng;
        private float _time;
        private float _readyAt;

        public int Capacity { get; }
        public int Held => _holders.Count;

        public AttackTokens(int capacity, Rng rng, float firstDelay = 0f)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            _rng = rng;
            _readyAt = firstDelay;
        }

        /// <summary>One token for a small pack, two once four or more creatures are in the fight.</summary>
        public static int CapacityFor(int packSize) => packSize >= 4 ? 2 : 1;

        /// <param name="deltaSeconds">Scaled time: the breather does not run while the world is paused.</param>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds > 0f) _time += deltaSeconds;
        }

        public bool Available => _holders.Count < Capacity && _time >= _readyAt;

        public bool Holds(object who) => _holders.Contains(who);

        public bool TryTake(object who)
        {
            if (_holders.Contains(who)) return true;
            if (!Available) return false;
            _holders.Add(who);
            return true;
        }

        public void Release(object who)
        {
            if (!_holders.Remove(who)) return;
            _readyAt = _time + _rng.Range(BattleConfig.TokenBreatherMinSeconds, BattleConfig.TokenBreatherMaxSeconds);
        }
    }
}

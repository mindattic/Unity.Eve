using System;
using UnityEve.Core.Util;

namespace UnityEve.Core.Battle
{
    public enum EnemyIntent
    {
        /// <summary>A beat of stillness between decisions: it stops, watches, then chooses.</summary>
        Think,
        /// <summary>Close to reach and look for an opening to strike.</summary>
        Approach,
        /// <summary>Work around toward the player's back for a better angle, then strike.</summary>
        Flank,
        /// <summary>Hang back and idle at range, waiting its turn.</summary>
        Hold,
        /// <summary>Stand still and power up (glows); afterwards it hits harder and moves faster.</summary>
        Buff,
        /// <summary>Back off when hurt or crowded.</summary>
        Retreat,
    }

    /// <summary>What a creature perceives this tick. Presentation measures it; the mind decides.</summary>
    public struct EnemySenses
    {
        public float Distance;
        public float HealthFraction;
        /// <summary>Cosine between the player's facing and the direction to this enemy: 1 = in front of her gun, -1 = behind her.</summary>
        public float PlayerFacing;
        /// <summary>The pack's committed-attacker limit has room.</summary>
        public bool AttackSlotFree;
        public int PackAlive;
    }

    /// <summary>How the presentation should move for the current intent.</summary>
    public readonly struct EnemySteering
    {
        /// <summary>Distance from the player to settle at, in metres; negative means "stay where you are".</summary>
        public readonly float RingDistance;
        /// <summary>Fraction of the creature's move speed.</summary>
        public readonly float Speed;
        /// <summary>0..1 weight of sideways movement around the player.</summary>
        public readonly float Orbit;
        /// <summary>Orbit toward the player's back instead of the creature's preferred circling side.</summary>
        public readonly bool TowardBack;

        public EnemySteering(float ringDistance, float speed, float orbit, bool towardBack)
        {
            RingDistance = ringDistance;
            Speed = speed;
            Orbit = orbit;
            TowardBack = towardBack;
        }

        public bool Still => Speed <= 0f;
    }

    /// <summary>
    /// The creature's tactical brain, layered over <see cref="EnemyAttackCycle"/>. Between attacks it
    /// stops to think, then picks an intent by weighted utility from its temperament (Aggression,
    /// Caution) and what it senses: press the attack, flank for a better angle, hold back, buff itself,
    /// or retreat. The attack cycle may only commit while the mind allows it. All rolls use the seeded Rng.
    /// </summary>
    public sealed class EnemyMind
    {
        private readonly EnemyStats _stats;
        private readonly Rng _rng;
        private float _timer;

        public EnemyIntent Intent { get; private set; } = EnemyIntent.Think;
        public float BuffRemaining { get; private set; }
        public float BuffCooldown { get; private set; }
        public bool Buffed => BuffRemaining > 0f;
        public float DamageMultiplier => Buffed ? BattleConfig.EnemyBuffDamageMultiplier : 1f;
        public float Tempo => Buffed ? BattleConfig.EnemyBuffTempo : 1f;

        /// <summary>0..1 through the buff cast, for the charge-up glow.</summary>
        public float BuffCastProgress =>
            Intent == EnemyIntent.Buff ? 1f - Math.Max(0f, _timer) / BattleConfig.EnemyBuffCastSeconds : 0f;

        public event Action<EnemyIntent> IntentChosen;
        public event Action BuffApplied;

        public EnemyMind(EnemyStats stats, Rng rng, float firstThinkSeconds = 0f)
        {
            _stats = stats;
            _rng = rng;
            _timer = firstThinkSeconds;
        }

        /// <param name="deltaSeconds">Scaled time: zero while the world is paused.</param>
        public void Tick(float deltaSeconds, in EnemySenses senses)
        {
            if (deltaSeconds <= 0f) return;
            BuffRemaining = Math.Max(0f, BuffRemaining - deltaSeconds);
            BuffCooldown = Math.Max(0f, BuffCooldown - deltaSeconds);
            _timer -= deltaSeconds;

            // A flanker that reaches the player's blind side seizes the opening at once.
            if (Intent == EnemyIntent.Flank && senses.PlayerFacing < BattleConfig.EnemyFlankOpeningFacing && senses.AttackSlotFree)
            {
                Choose(EnemyIntent.Approach, _rng.Range(1.5f, 2.5f));
                return;
            }

            if (_timer > 0f) return;
            if (Intent == EnemyIntent.Buff)
            {
                BuffRemaining = BattleConfig.EnemyBuffSeconds;
                BuffCooldown = BattleConfig.EnemyBuffCooldownSeconds;
                BuffApplied?.Invoke();
            }

            if (Intent == EnemyIntent.Think) Choose(Decide(senses));
            else Pause();
        }

        /// <summary>After a strike, a stagger or a stun: stop and think again.</summary>
        public void Reconsider() => Pause();

        /// <summary>The attack cycle may wind up only while this is true.</summary>
        public bool MayAttack(in EnemySenses senses)
        {
            switch (Intent)
            {
                case EnemyIntent.Approach: return true;
                case EnemyIntent.Flank: return senses.PlayerFacing < BattleConfig.EnemyFlankOpeningFacing;
                // An idling creature still snaps at a player who walks right up to it.
                case EnemyIntent.Hold: return senses.Distance <= _stats.Reach * 0.8f;
                default: return false;
            }
        }

        public EnemySteering Steer(in EnemySenses senses)
        {
            var reach = _stats.Reach;
            switch (Intent)
            {
                case EnemyIntent.Approach: return new EnemySteering(reach * 0.92f, 1f, 0.25f, false);
                case EnemyIntent.Flank: return new EnemySteering(Math.Max(reach * 1.6f, 1.6f), 0.95f, 1f, true);
                case EnemyIntent.Hold: return new EnemySteering(-1f, 0f, 0f, false);
                case EnemyIntent.Retreat: return new EnemySteering(Math.Max(reach * 3.2f, 3.5f), 0.85f, 0.3f, false);
                default: return new EnemySteering(-1f, 0f, 0f, false); // Think, Buff: stand still
            }
        }

        /// <summary>Weighted utility pick. Exposed for tests.</summary>
        public EnemyIntent Decide(in EnemySenses s)
        {
            var reach = _stats.Reach;
            var aggression = Clamp01(_stats.Aggression);
            var caution = Clamp01(_stats.Caution);
            var hurt = s.HealthFraction < 0.4f;
            var far = s.Distance > reach * 3f;

            var approach = 0.6f + 1.6f * aggression + (Buffed ? 1f : 0f);
            if (!s.AttackSlotFree) approach *= 0.25f;
            if (hurt) approach *= 1f - 0.6f * caution;

            var flank = (s.PlayerFacing > 0.3f ? 0.9f : 0.25f) + 0.6f * caution;
            if (far) flank *= 0.4f;

            var hold = 0.25f + 0.7f * caution + (s.AttackSlotFree ? 0f : 0.9f) + (s.PackAlive >= 3 ? 0.3f : 0f);

            var buff = 0f;
            if (!Buffed && BuffCooldown <= 0f && s.Distance > reach * 1.6f)
                buff = 0.25f + 0.6f * caution + (hurt ? 0.5f : 0f) + (s.PackAlive > 1 ? 0.2f : 0f);

            var retreat = hurt ? 1.4f * caution : (s.Distance < reach * 0.7f ? 0.3f * caution : 0f);

            var weights = new[] { approach, flank, hold, buff, retreat };
            var intents = new[] { EnemyIntent.Approach, EnemyIntent.Flank, EnemyIntent.Hold, EnemyIntent.Buff, EnemyIntent.Retreat };
            var total = 0f;
            foreach (var w in weights) total += w;
            var roll = _rng.NextFloat() * total;
            for (var i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0f) return intents[i];
            }
            return EnemyIntent.Approach;
        }

        private void Pause() => Choose(EnemyIntent.Think, _rng.Range(BattleConfig.EnemyThinkMinSeconds, BattleConfig.EnemyThinkMaxSeconds));

        private void Choose(EnemyIntent intent)
        {
            float seconds;
            switch (intent)
            {
                case EnemyIntent.Approach: seconds = _rng.Range(2.2f, 4f); break;
                case EnemyIntent.Flank: seconds = _rng.Range(1.6f, 3f); break;
                case EnemyIntent.Hold: seconds = _rng.Range(0.8f, 1.8f); break;
                case EnemyIntent.Buff: seconds = BattleConfig.EnemyBuffCastSeconds; break;
                case EnemyIntent.Retreat: seconds = _rng.Range(1f, 1.8f); break;
                default: seconds = _rng.Range(BattleConfig.EnemyThinkMinSeconds, BattleConfig.EnemyThinkMaxSeconds); break;
            }
            Choose(intent, seconds);
        }

        private void Choose(EnemyIntent intent, float seconds)
        {
            Intent = intent;
            _timer = seconds;
            IntentChosen?.Invoke(intent);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}

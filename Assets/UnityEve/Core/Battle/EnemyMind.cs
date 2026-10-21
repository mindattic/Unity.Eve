using System;
using Unity.Eve.Core.Util;

namespace Unity.Eve.Core.Battle
{
    public enum EnemyIntent
    {
        /// <summary>A beat of stillness between decisions: it stops, watches, then chooses.</summary>
        Think,
        /// <summary>Hang back on its ring around the player and idle.</summary>
        Lurk,
        /// <summary>Pace slowly around the player at a distance.</summary>
        Circle,
        /// <summary>Stand still and power up (glows); afterwards it hits harder and moves faster.</summary>
        Buff,
        /// <summary>Token holder: close in at a prowl to charging distance.</summary>
        Stalk,
        /// <summary>Token holder: work round toward the player's back first.</summary>
        Flank,
        /// <summary>Token holder: rear up for a beat, then charge in at a burst of speed and strike.</summary>
        Charge,
        /// <summary>Back off to its ring: after a strike, when crowding the player, or when hurt.</summary>
        Retreat,
    }

    /// <summary>What a creature perceives this tick. Presentation measures it; the mind decides.</summary>
    public struct EnemySenses
    {
        public float Distance;
        public float HealthFraction;
        /// <summary>Cosine between the player's facing and the direction to this enemy: 1 = in front of her gun, -1 = behind her.</summary>
        public float PlayerFacing;
        public int PackAlive;
    }

    /// <summary>How the presentation should move for the current intent.</summary>
    public readonly struct EnemySteering
    {
        /// <summary>Distance from the player to settle at, in metres; negative means "stay where you are".</summary>
        public readonly float RingDistance;
        /// <summary>Multiple of the creature's move speed (above 1 for a charge).</summary>
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
    /// The creature's tactical brain, layered over <see cref="EnemyAttackCycle"/>. Creatures take turns
    /// through the pack's <see cref="AttackTokens"/>: the one holding a token stalks or flanks in, rears up,
    /// charges, strikes and retreats, then gives the token back. Everyone else keeps to a ring around the
    /// player and lurks, circles or powers up, backing off if crowded. Every intent ends in a short pause
    /// to think. Temperament (Aggression, Caution) weights the choices; all rolls use the seeded Rng.
    /// </summary>
    public sealed class EnemyMind
    {
        private readonly EnemyStats _stats;
        private readonly Rng _rng;
        private readonly Func<bool> _takeToken;
        private readonly Action _releaseToken;
        private float _timer;
        private float _chargeAnticipation;

        public EnemyIntent Intent { get; private set; } = EnemyIntent.Think;
        public bool HasToken { get; private set; }
        /// <summary>This creature's preferred distance from the player while it waits its turn.</summary>
        public float RingRadius { get; }
        public float BuffRemaining { get; private set; }
        public float BuffCooldown { get; private set; }
        public bool Buffed => BuffRemaining > 0f;
        public float DamageMultiplier => Buffed ? BattleConfig.EnemyBuffDamageMultiplier : 1f;
        public float Tempo => Buffed ? BattleConfig.EnemyBuffTempo : 1f;
        /// <summary>True during the beat a charging creature rears up before it runs.</summary>
        public bool RearingUp => Intent == EnemyIntent.Charge && _chargeAnticipation > 0f;

        /// <summary>0..1 through the buff cast, for the charge-up glow.</summary>
        public float BuffCastProgress =>
            Intent == EnemyIntent.Buff ? 1f - Math.Max(0f, _timer) / BattleConfig.EnemyBuffCastSeconds : 0f;

        public event Action<EnemyIntent> IntentChosen;
        public event Action BuffApplied;

        /// <param name="takeToken">Asks the pack for an attack token; null means always granted (tests, lone creatures).</param>
        /// <param name="releaseToken">Gives the token back.</param>
        public EnemyMind(EnemyStats stats, Rng rng, float firstThinkSeconds = 0f, Func<bool> takeToken = null, Action releaseToken = null)
        {
            _stats = stats;
            _rng = rng;
            _timer = firstThinkSeconds;
            _takeToken = takeToken ?? (() => true);
            _releaseToken = releaseToken ?? (() => { });
            RingRadius = Math.Max(_rng.Range(BattleConfig.EnemyRingMinRadius, BattleConfig.EnemyRingMaxRadius), stats.Reach * 2.6f);
        }

        /// <param name="deltaSeconds">Scaled time: zero while the world is paused.</param>
        public void Tick(float deltaSeconds, in EnemySenses s)
        {
            if (deltaSeconds <= 0f) return;
            BuffRemaining = Math.Max(0f, BuffRemaining - deltaSeconds);
            BuffCooldown = Math.Max(0f, BuffCooldown - deltaSeconds);
            _timer -= deltaSeconds;
            var reach = _stats.Reach;

            switch (Intent)
            {
                case EnemyIntent.Stalk:
                    if (s.Distance <= ChargeDistance) { Choose(EnemyIntent.Charge); return; }
                    if (_timer <= 0f) { Choose(EnemyIntent.Charge); return; }
                    break;
                case EnemyIntent.Flank:
                    if (s.PlayerFacing < BattleConfig.EnemyFlankOpeningFacing || _timer <= 0f) { Choose(EnemyIntent.Charge); return; }
                    break;
                case EnemyIntent.Charge:
                    _chargeAnticipation -= deltaSeconds;
                    // Missed its chance (the player kept away): give up the turn and fall back.
                    if (_timer <= 0f) { GiveBackToken(); Choose(EnemyIntent.Retreat); }
                    return;
                case EnemyIntent.Retreat:
                    if (s.Distance >= RingRadius * 0.9f || _timer <= 0f) Pause();
                    return;
                case EnemyIntent.Buff:
                    if (_timer <= 0f)
                    {
                        BuffRemaining = BattleConfig.EnemyBuffSeconds;
                        BuffCooldown = BattleConfig.EnemyBuffCooldownSeconds;
                        BuffApplied?.Invoke();
                        Pause();
                    }
                    return;
                default: // Think, Lurk, Circle
                    // A waiting creature crowding the player backs off to its ring.
                    if (Intent != EnemyIntent.Think && s.Distance < Math.Max(reach * 1.4f, RingRadius * 0.55f)) { Choose(EnemyIntent.Retreat); return; }
                    if (_timer > 0f) return;
                    if (Intent == EnemyIntent.Think) Choose(Decide(s));
                    else Pause();
                    return;
            }
        }

        private float ChargeDistance => Math.Max(_stats.Reach * 2.4f, 2.4f);

        /// <summary>The strike landed (hit, whiff or evaded): give the turn back and fall back to the ring.</summary>
        public void Struck()
        {
            GiveBackToken();
            Choose(EnemyIntent.Retreat);
        }

        /// <summary>Stunned or staggered: lose the turn and think again.</summary>
        public void Reconsider()
        {
            GiveBackToken();
            Pause();
        }

        /// <summary>Dead or removed: make sure the pack gets its token back.</summary>
        public void Leave() => GiveBackToken();

        /// <summary>The attack cycle may wind up only while this is true: a charging token holder.</summary>
        public bool MayAttack() => HasToken && Intent == EnemyIntent.Charge && !RearingUp;

        public EnemySteering Steer()
        {
            switch (Intent)
            {
                case EnemyIntent.Lurk: return new EnemySteering(RingRadius, 0.4f, 0.1f, false);
                case EnemyIntent.Circle: return new EnemySteering(RingRadius, 0.45f, 1f, false);
                case EnemyIntent.Stalk: return new EnemySteering(ChargeDistance * 0.9f, 0.7f, 0.15f, false);
                case EnemyIntent.Flank: return new EnemySteering(Math.Max(ChargeDistance, RingRadius * 0.6f), 0.95f, 1f, true);
                case EnemyIntent.Charge:
                    return RearingUp
                        ? new EnemySteering(-1f, 0f, 0f, false)
                        : new EnemySteering(_stats.Reach * 0.8f, BattleConfig.EnemyChargeSpeed, 0f, false);
                case EnemyIntent.Retreat: return new EnemySteering(RingRadius, BattleConfig.EnemyRetreatSpeed, 0.25f, false);
                default: return new EnemySteering(-1f, 0f, 0f, false); // Think, Buff: stand still
            }
        }

        /// <summary>The next intent for a creature that is free to choose. Exposed for tests.</summary>
        public EnemyIntent Decide(in EnemySenses s)
        {
            var aggression = Clamp01(_stats.Aggression);
            var caution = Clamp01(_stats.Caution);
            var hurt = s.HealthFraction < 0.4f;

            // Try for a turn to attack.
            var eagerness = 0.3f + 0.55f * aggression + (Buffed ? 0.3f : 0f) - (hurt ? 0.35f * caution : 0f);
            if (_rng.Chance(Clamp01(eagerness)) && _takeToken())
            {
                HasToken = true;
                if (s.Distance <= ChargeDistance) return EnemyIntent.Charge;
                return s.PlayerFacing > 0.3f && _rng.Chance(0.2f + 0.5f * caution) ? EnemyIntent.Flank : EnemyIntent.Stalk;
            }

            // Waiting its turn.
            var lurk = 0.5f + 0.7f * caution;
            var circle = 0.7f;
            var buff = !Buffed && BuffCooldown <= 0f ? 0.15f + 0.5f * caution + (hurt ? 0.4f : 0f) : 0f;
            var retreat = hurt ? 1.2f * caution : 0f;
            var total = lurk + circle + buff + retreat;
            var roll = _rng.NextFloat() * total;
            if ((roll -= lurk) < 0f) return EnemyIntent.Lurk;
            if ((roll -= circle) < 0f) return EnemyIntent.Circle;
            if ((roll -= buff) < 0f) return EnemyIntent.Buff;
            return EnemyIntent.Retreat;
        }

        private void GiveBackToken()
        {
            if (!HasToken) return;
            HasToken = false;
            _releaseToken();
        }

        private void Pause() => Choose(EnemyIntent.Think, _rng.Range(BattleConfig.EnemyThinkMinSeconds, BattleConfig.EnemyThinkMaxSeconds));

        private void Choose(EnemyIntent intent)
        {
            float seconds;
            switch (intent)
            {
                case EnemyIntent.Lurk: seconds = _rng.Range(1.5f, 3.2f); break;
                case EnemyIntent.Circle: seconds = _rng.Range(1.8f, 3.5f); break;
                case EnemyIntent.Buff: seconds = BattleConfig.EnemyBuffCastSeconds; break;
                case EnemyIntent.Stalk: seconds = _rng.Range(2.5f, 4f); break;
                case EnemyIntent.Flank: seconds = _rng.Range(2f, 3.2f); break;
                case EnemyIntent.Charge:
                    _chargeAnticipation = BattleConfig.EnemyChargeAnticipationSeconds;
                    seconds = BattleConfig.EnemyChargeAnticipationSeconds + BattleConfig.EnemyChargeMaxSeconds;
                    break;
                case EnemyIntent.Retreat: seconds = _rng.Range(1.2f, 2f); break;
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

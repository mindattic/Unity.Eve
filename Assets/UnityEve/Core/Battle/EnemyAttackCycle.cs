using System;

namespace Unity.Eve.Core.Battle
{
    public enum AttackPhase
    {
        /// <summary>Closing distance or circling the player.</summary>
        Close,
        /// <summary>Telegraph: the enemy glows and coils. The world-pause freezes it mid-telegraph.</summary>
        Windup,
        /// <summary>A short lunge toward where the player was when the windup began.</summary>
        Strike,
        Recover,
    }

    /// <summary>
    /// The four-beat enemy attack rhythm from ExperimentEve: close → windup → strike → recover.
    /// Pure timing; the presentation layer owns positions and reports distance each tick.
    /// </summary>
    public sealed class EnemyAttackCycle
    {
        private readonly EnemyStats _stats;
        private float _phaseTimer;

        public AttackPhase Phase { get; private set; } = AttackPhase.Close;
        public float Cooldown { get; private set; }

        /// <summary>0 at the start of the windup, 1 at the strike. Drives the telegraph glow.</summary>
        public float WindupProgress =>
            Phase == AttackPhase.Windup && _stats.WindupSeconds > 0f ? 1f - _phaseTimer / _stats.WindupSeconds : 0f;

        public bool IsCommitted => Phase == AttackPhase.Windup || Phase == AttackPhase.Strike;

        /// <summary>Seconds until <see cref="StrikeLanded"/> fires, across windup and strike; +infinity if not committed.</summary>
        public float SecondsUntilStrike => Phase switch
        {
            AttackPhase.Windup => _phaseTimer + BattleConfig.StrikeSeconds,
            AttackPhase.Strike => _phaseTimer,
            _ => float.PositiveInfinity,
        };

        public event Action WindupStarted;
        /// <summary>Raised when the strike lands; the listener resolves hit/whiff/evade.</summary>
        public event Action StrikeLanded;

        public EnemyAttackCycle(EnemyStats stats, float initialCooldown)
        {
            _stats = stats;
            Cooldown = initialCooldown;
        }

        /// <param name="deltaSeconds">Scaled (game) time: zero while the world is paused.</param>
        /// <param name="distanceToPlayer">Current distance from the enemy to the player.</param>
        /// <param name="mayCommit">False when the pack's attacker limit is already reached.</param>
        /// <param name="tempo">Attack tempo multiplier (Slow PE = 0.5).</param>
        public void Tick(float deltaSeconds, float distanceToPlayer, bool mayCommit, float tempo = 1f)
        {
            if (deltaSeconds <= 0f) return;
            var dt = deltaSeconds * tempo;
            switch (Phase)
            {
                case AttackPhase.Close:
                    Cooldown = Math.Max(0f, Cooldown - dt);
                    if (distanceToPlayer <= _stats.Reach && mayCommit && Cooldown <= 0f)
                    {
                        Phase = AttackPhase.Windup;
                        _phaseTimer = _stats.WindupSeconds;
                        WindupStarted?.Invoke();
                    }
                    break;
                case AttackPhase.Windup:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        Phase = AttackPhase.Strike;
                        _phaseTimer = BattleConfig.StrikeSeconds;
                    }
                    break;
                case AttackPhase.Strike:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        StrikeLanded?.Invoke();
                        Phase = AttackPhase.Recover;
                        _phaseTimer = _stats.RecoverSeconds;
                    }
                    break;
                case AttackPhase.Recover:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        Phase = AttackPhase.Close;
                        Cooldown = _stats.CooldownSeconds;
                    }
                    break;
            }
        }

        /// <summary>A stun or stagger cancels the swing in progress.</summary>
        public void Interrupt(float minimumCooldown = 0.35f)
        {
            Phase = AttackPhase.Close;
            Cooldown = Math.Max(Cooldown, minimumCooldown);
        }

        /// <summary>How a landed strike resolves against the player.</summary>
        public static StrikeOutcome Resolve(float distanceToPlayer, float reach, bool playerInvulnerable)
        {
            if (distanceToPlayer > reach + BattleConfig.StrikeWhiffSlack) return StrikeOutcome.Whiff;
            return playerInvulnerable ? StrikeOutcome.Evaded : StrikeOutcome.Hit;
        }
    }

    public enum StrikeOutcome
    {
        Hit,
        Whiff,
        Evaded,
    }
}

using System;
using System.Collections.Generic;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Util;

namespace Unity.Eve.Core.Sim
{
    /// <summary>
    /// A kind of creature as the battle authority sees it: its rules plus the two body measurements its
    /// prefab carries. <see cref="Token"/> is presentation's own handle for it (the definition asset to
    /// instantiate); the authority never looks inside.
    /// </summary>
    public sealed class EnemyKind
    {
        public EnemyKind(EnemyStats stats, float bodyRadius, float aimHeight, EnemyKind brood = null, object token = null)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            BodyRadius = bodyRadius;
            AimHeight = aimHeight;
            Brood = brood;
            Token = token;
        }

        public EnemyStats Stats { get; }
        public float BodyRadius { get; }
        /// <summary>Height of the weak point above its feet, before any hover.</summary>
        public float AimHeight { get; }
        /// <summary>What a boss calls up as its health falls; null for none.</summary>
        public EnemyKind Brood { get; }
        public object Token { get; }
    }

    /// <summary>The battle arena on the ground plane: creatures are kept inside it.</summary>
    public readonly struct Arena
    {
        public Arena(Vec3 min, Vec3 max)
        {
            Min = min;
            Max = max;
        }

        public Vec3 Min { get; }
        public Vec3 Max { get; }
        public Vec3 Size => Max - Min;
    }

    /// <summary>
    /// A creature in battle, owned by the authority. Real-time behaviour on scaled time: its
    /// <see cref="EnemyMind"/> takes turns with the pack through attack tokens; with a token it stalks or
    /// flanks in, rears up, charges, strikes and retreats; without one it lurks, circles or powers up on a ring
    /// around the player. When it commits it telegraphs during its windup, lunges at where the player was,
    /// then recovers. Position and facing live here; <c>EnemyActor</c> only shows them.
    /// </summary>
    public sealed class EnemyState
    {
        private readonly SimClock _clock;
        private readonly StaggerMeter _stagger = new StaggerMeter();
        private EnemyAttackCycle _cycle;
        private EnemyMind _mind;
        private Func<int> _packAlive;
        private Arena _arena;
        private PlayerBody _target;
        private Func<bool> _mayCommit;
        private Action<EnemyState> _onStrike;
        private Action<EnemyState> _onDied;
        private float _stunnedUntil;
        private float _slowUntil;
        private float _circleSign = 1f;
        private Vec3 _anchor;

        internal EnemyState(int id, EnemyKind kind, Vec3 position, float yaw, SimClock clock)
        {
            Id = id;
            Kind = kind;
            Position = PreviousPosition = position;
            Yaw = PreviousYaw = yaw;
            _clock = clock;
            Health = new Health(kind.Stats.MaxHp);
        }

        /// <summary>Stable within its battle: commands name targets by it.</summary>
        public int Id { get; }
        public EnemyKind Kind { get; }
        public EnemyStats Stats => Kind.Stats;
        public float BodyRadius => Kind.BodyRadius;
        public Health Health { get; }
        public Vec3 Position { get; private set; }
        public float Yaw { get; private set; }
        /// <summary>Where it was at the start of the last tick (presentation interpolates from here).</summary>
        public Vec3 PreviousPosition { get; private set; }
        public float PreviousYaw { get; private set; }
        public Vec3 Forward => SimMath.Forward(Yaw);
        /// <summary>Its weak point: the hover it is at now, plus its kind's hover and aim height.</summary>
        public Vec3 AimPoint => Position + Vec3.Up * (Kind.AimHeight + Stats.HoverHeight);

        public bool IsAlive => !Health.IsDead;
        public bool IsCommitted => _cycle != null && _cycle.IsCommitted;
        public AttackPhase Phase => _cycle != null ? _cycle.Phase : AttackPhase.Close;
        public float WindupProgress => _cycle != null ? _cycle.WindupProgress : 0f;
        public EnemyIntent Intent => _mind != null ? _mind.Intent : EnemyIntent.Think;
        public bool Buffed => _mind != null && _mind.Buffed;
        public float BuffCastProgress => _mind != null ? _mind.BuffCastProgress : 0f;
        public bool RearingUp => _mind != null && _mind.RearingUp;
        /// <summary>Scaled seconds until its committed strike lands; +infinity if it isn't attacking.</summary>
        public float SecondsUntilStrike => _cycle != null ? _cycle.SecondsUntilStrike : float.PositiveInfinity;
        /// <summary>Strike damage multiplier (an enraged creature hits harder).</summary>
        public float DamageMultiplier => _mind != null ? _mind.DamageMultiplier : 1f;
        public bool Stunned => _clock.Now < _stunnedUntil;
        /// <summary>Scaled time it appeared (it materializes over 0.4 s).</summary>
        public float SpawnedAt { get; private set; }
        /// <summary>0..1 through materializing.</summary>
        public float Grown => SimMath.Clamp01((_clock.Now - SpawnedAt) / 0.4f);
        /// <summary>Scaled time it died; -1 while alive.</summary>
        public float DiedAt { get; private set; } = -1f;
        /// <summary>Attack tempo of the last tick (Slow, enrage).</summary>
        public float Tempo { get; private set; } = 1f;
        /// <summary>Ground speed it actually covered over the last tick.</summary>
        public float Speed { get; private set; }

        /// <summary>A windup began: the telegraph (presentation glows and plays the attack animation).</summary>
        public event Action<EnemyState> WindupStarted;
        /// <summary>Its strike landed; raised before the battle resolves hit, whiff or evade.</summary>
        public event Action<EnemyState> StrikeLanded;
        public event Action<EnemyState, EnemyIntent> IntentChosen;
        public event Action<EnemyState> BuffApplied;
        /// <summary>Raised with the damage a hit dealt (before any death).</summary>
        public event Action<EnemyState, int> Hit;
        public event Action<EnemyState> Died;

        /// <summary>The creature materializes and starts thinking. The draw order from <paramref name="seed"/>
        /// (first think, ring radius, circling side) is part of a replay.</summary>
        internal void Begin(PlayerBody target, Func<bool> mayCommit, float initialCooldown, uint seed, Arena arena,
            Func<int> packAlive, AttackTokens tokens, Action<EnemyState> onStrike, Action<EnemyState> onDied)
        {
            _arena = arena;
            _packAlive = packAlive;
            _onStrike = onStrike;
            _onDied = onDied;
            var rng = new Rng(seed);
            // Bosses do not queue for the pack's attack token: the Mother attacks whenever she can reach.
            var queue = tokens != null && !Stats.Boss;
            _mind = new EnemyMind(Stats, rng, rng.Range(0.4f, 1.2f),
                queue ? () => tokens.TryTake(this) : (Func<bool>)null,
                queue ? () => tokens.Release(this) : (Action)null);
            _mind.IntentChosen += i => IntentChosen?.Invoke(this, i);
            _mind.BuffApplied += () => BuffApplied?.Invoke(this);
            _target = target;
            _mayCommit = mayCommit;
            _cycle = new EnemyAttackCycle(Stats, initialCooldown);
            _cycle.WindupStarted += () =>
            {
                _anchor = _target.Position;
                WindupStarted?.Invoke(this);
            };
            _cycle.StrikeLanded += () =>
            {
                StrikeLanded?.Invoke(this);
                _onStrike?.Invoke(this);
                _mind.Struck();
            };
            _circleSign = rng.Chance(0.5f) ? -1f : 1f;
            SpawnedAt = _clock.Now;
        }

        public void ApplyStun(float seconds)
        {
            _stunnedUntil = Math.Max(_stunnedUntil, _clock.Now + seconds);
            _cycle?.Interrupt();
            _mind?.Reconsider();
        }

        public void ApplySlow(float seconds) => _slowUntil = Math.Max(_slowUntil, _clock.Now + seconds);

        public void Knockback(Vec3 from, float metres)
        {
            var d = (Position - from).Flat;
            if (d.SqrMagnitude <= 0.0001f) return;
            Position += d.Normalized * metres;
            // A long knockback also stuns, which skips movement (and its arena clamp) for the whole stun:
            // keep it inside the arena now so a shove near a wall never leaves it outside.
            KeepInArena();
        }

        /// <returns>Damage dealt.</returns>
        public int TakeHit(int amount)
        {
            if (!IsAlive) return 0;
            var dealt = Health.Damage(amount);
            Hit?.Invoke(this, dealt);
            if (_stagger.AddHit(dealt, Stats.MaxHp, stunned: _clock.Now < _stunnedUntil))
                ApplyStun(BattleConfig.StaggerStunSeconds);
            if (Health.IsDead)
            {
                DiedAt = _clock.Now;
                _mind?.Leave();
                Died?.Invoke(this);
                _onDied?.Invoke(this);
            }
            return dealt;
        }

        /// <summary>Pushes overlapping pack members apart and off the player (every tick, even paused).</summary>
        internal static void Separate(IReadOnlyList<EnemyState> pack, Vec3 player)
        {
            for (var i = 0; i < pack.Count; i++)
            {
                var a = pack[i];
                if (!a.IsAlive) continue;
                for (var j = i + 1; j < pack.Count; j++)
                {
                    var b = pack[j];
                    if (!b.IsAlive) continue;
                    var d = (b.Position - a.Position).Flat;
                    var min = a.BodyRadius + b.BodyRadius + 0.14f;
                    var dist = d.Magnitude;
                    if (dist >= min || dist < 0.0001f) continue;
                    var push = d / dist * ((min - dist) * 0.5f);
                    a.Position -= push;
                    b.Position += push;
                }
                var toPlayer = (a.Position - player).Flat;
                var minP = a.BodyRadius + 0.4f;
                if (toPlayer.Magnitude < minP && toPlayer.SqrMagnitude > 0.0001f)
                    a.Position = player + toPlayer.Normalized * minP + Vec3.Up * (a.Position.Y - player.Y);
            }
        }

        /// <param name="dt">Scaled tick seconds: 0 while the world is paused.</param>
        internal void Tick(float dt)
        {
            PreviousPosition = Position;
            PreviousYaw = Yaw;
            if (_cycle == null || !IsAlive) return;
            var now = _clock.Now;
            var grown = Grown;

            var stunned = now < _stunnedUntil;
            _stagger.Tick(dt, stunned);
            var before = Position;
            var tempo = now < _slowUntil ? 0.5f : 1f;
            var toPlayer = (_target.Position - Position).Flat;
            var dist = toPlayer.Magnitude;
            var dir = dist > 0.0001f ? toPlayer / dist : Forward;

            var facing = _target.Forward.Flat;
            var senses = new EnemySenses
            {
                Distance = dist,
                HealthFraction = Health.Fraction,
                PlayerFacing = facing.SqrMagnitude > 0.0001f ? Vec3.Dot(facing.Normalized, -dir) : 0f,
                PackAlive = _packAlive != null ? _packAlive() : 1,
            };
            tempo *= _mind.Tempo;

            if (!stunned && grown >= 1f)
            {
                if (_cycle.Phase == AttackPhase.Close) _mind.Tick(dt, senses);
                // Tokens decide whose turn it is; the commit limit stays as a safety net.
                _cycle.Tick(dt, dist, (_mayCommit == null || _mayCommit()) && _mind.MayAttack(), tempo);
                Move(dt, dir, dist, tempo, _mind.Steer(), facing);
            }

            var moved = (Position - before).Flat;
            Speed = dt > 0f ? moved.Magnitude / dt : 0f;
            Tempo = tempo;

            var y = Stats.HoverHeight > 0f
                ? (_cycle.IsCommitted ? 0.75f : Stats.HoverHeight + SimMath.Sin(now * 2.4f) * 0.1f)
                : 0f;
            Position = Position.WithY(SimMath.Lerp(Position.Y, y, 1f - SimMath.Exp(-8f * dt)));
        }

        private void Move(float dt, Vec3 dir, float dist, float tempo, EnemySteering steering, Vec3 playerFacing)
        {
            switch (_cycle.Phase)
            {
                case AttackPhase.Close:
                {
                    // Always watch the player, even while idling, thinking or powering up.
                    Turn(dir, 9f * dt);
                    if (steering.Still) break;
                    var speed = Stats.MoveSpeed * tempo * steering.Speed;
                    var tangent = new Vec3(-dir.Z, 0f, dir.X) * _circleSign;
                    if (steering.TowardBack)
                    {
                        // Go the way round that leads toward her back.
                        tangent = new Vec3(-dir.Z, 0f, dir.X);
                        if (Vec3.Dot(tangent, -playerFacing) < 0f) tangent = -tangent;
                    }
                    // Radial error: positive means too far (move in), negative means too close (back off).
                    var error = steering.RingDistance >= 0f ? dist - steering.RingDistance : 0f;
                    var radial = dir * SimMath.Clamp(error, -1f, 1f);
                    var wish = radial + tangent * steering.Orbit * (Math.Abs(error) < 1.5f ? 1f : 0.3f);
                    if (wish.SqrMagnitude > 0.0004f)
                        Position += wish.Normalized * (speed * SimMath.Clamp01(wish.Magnitude + 0.35f) * dt);
                    KeepInArena();
                    break;
                }
                case AttackPhase.Windup:
                    Turn(dir, 2.5f * dt);
                    Position -= dir * (Stats.MoveSpeed * 0.18f * dt);
                    break;
                case AttackPhase.Strike:
                {
                    // A ranged creature spits from where it stood at the windup; only melee lunges in.
                    if (Stats.Ranged) break;
                    var lunge = (_anchor - Position).Flat;
                    if (lunge.Magnitude > Stats.Reach * 0.5f)
                        Position += lunge.Normalized * (Stats.MoveSpeed * 3.2f * dt);
                    break;
                }
            }
        }

        private void KeepInArena()
        {
            if (_arena.Size.SqrMagnitude < 0.01f) return;
            var p = Position;
            var x = SimMath.Clamp(p.X, _arena.Min.X + BodyRadius + 0.3f, _arena.Max.X - BodyRadius - 0.3f);
            var z = SimMath.Clamp(p.Z, _arena.Min.Z + BodyRadius + 0.3f, _arena.Max.Z - BodyRadius - 0.3f);
            Position = new Vec3(x, p.Y, z);
        }

        private void Turn(Vec3 dir, float t)
        {
            if (dir.SqrMagnitude < 0.0001f) return;
            Yaw = SimMath.TurnToward(Yaw, SimMath.YawOf(dir), SimMath.Clamp01(t));
        }

        /// <summary>A line of numbers that changes whenever the creature's state does (replay comparison).</summary>
        public string Fingerprint() => FormattableString.Invariant(
            $"#{Id} {Stats.Id} hp {Health.Current} at {Position} yaw {Yaw:R} {Phase} {Intent} stun {_stunnedUntil:R} slow {_slowUntil:R}");
    }
}

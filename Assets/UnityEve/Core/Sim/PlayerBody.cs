using System;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Camera;
using Unity.Eve.Core.Session;

namespace Unity.Eve.Core.Sim
{
    /// <summary>How a player's body handles (the PlayerController's tuning, passed in from its prefab).</summary>
    public sealed class BodySettings
    {
        public float TurnSpeed = 12f;
        public float Acceleration = 26f;
        public float Deceleration = 34f;
        /// <summary>Minimum fraction of run speed she's eased down to right up against something in her path.</summary>
        public float ObstacleMinSpeedFactor = 0.3f;
    }

    /// <summary>What the body needs to know about the fight around it (the battle authority answers).</summary>
    public interface IThreats
    {
        bool InBattle { get; }
        /// <summary>Scaled seconds since her last gun shot, Slash or Rush.</summary>
        float SecondsSinceLastAttack { get; }
        /// <summary>Whoever she last attacked, alive or not; null before her first attack this battle.</summary>
        EnemyState LastAttackTarget { get; }
        /// <summary>The nearest living enemy to <paramref name="from"/>; null outside battle or with none alive.</summary>
        EnemyState NearestLivingEnemy(Vec3 from);
    }

    /// <summary>
    /// A player's body, owned by the authority: camera-relative movement with a direction latch across fixed
    /// camera cuts, the dodge leap and its invulnerability frames, roots and locks, the Max Payne shootdodge,
    /// backpedalling away from a target and leaning out of cover. It steps once per fixed tick on scaled time
    /// (so it freezes with the world-pause); only the shootdodge leap itself runs on the tick's real time.
    /// Collision is the one thing it asks of <see cref="IBodyPhysics"/>.
    /// </summary>
    public sealed class PlayerBody
    {
        private readonly MovementBasis _basis = new MovementBasis();
        private readonly BodySettings _settings;
        private readonly IBodyPhysics _physics;
        private readonly SimClock _clock;
        private Vec3 _velocity;
        private float _verticalSpeed;
        private float _rootedUntil;
        private float _dodgeEnds = -1f;
        private float _dodgeStarted = -10f;
        private float _invulnerableUntil;
        private float _dodgeReadyAt;
        private Vec3 _dodgeDirection;
        private int _locks;
        private Vec3 _animLastPosition;
        private float _shootdodgeVerticalSpeed;
        private Vec3 _shootdodgeDirection;
        private float _shootdodgeReadyAt = -999f;
        private float _yaw;

        public PlayerBody(PlayerState state, IBodyPhysics physics, SimClock clock, BodySettings settings = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _physics = physics ?? throw new ArgumentNullException(nameof(physics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? new BodySettings();
            _animLastPosition = physics.Position;
        }

        public PlayerState State { get; }
        public PlayerId Seat => State.Seat;
        /// <summary>The fight around her, if any (set by the authority).</summary>
        public IThreats Threats { get; set; }
        /// <summary>Level geometry queries (cover); nothing gives cover without one.</summary>
        public IWorldQueries World { get; set; } = OpenWorld.Instance;

        private float Now => _clock.Now;

        public Vec3 Position => _physics.Position;
        /// <summary>Facing, radians clockwise from +Z.</summary>
        public float Yaw
        {
            get => _yaw;
            private set
            {
                _yaw = value;
                _physics.SetYaw(value);
            }
        }
        public Vec3 Forward => SimMath.Forward(_yaw);
        public Vec3 Velocity => _velocity;

        public bool IsDodging => Now < _dodgeEnds;
        /// <summary>Max Payne shootdodge: airborne, committed to a straight leap, untouchable, firing.</summary>
        public bool IsShootdodging { get; private set; }
        public bool ShootdodgeReady => Now >= _shootdodgeReadyAt;
        /// <summary>This tick's camera-relative movement turned into a world direction (what the stick is asking
        /// for right now): the leap direction when a shootdodge starts.</summary>
        public Vec3 CurrentWish { get; private set; }
        public bool IsInvulnerable => Now < _invulnerableUntil || IsShootdodging;
        /// <summary>Scaled seconds since the most recent dodge started (for perfect-dodge timing).</summary>
        public float SecondsSinceDodge => Now - _dodgeStarted;
        public bool CanAct => _locks == 0 && Now >= _rootedUntil && !IsDodging && !IsShootdodging && !State.IsDead;
        /// <summary>True when the dodge button should be honoured.</summary>
        public bool DodgeEnabled { get; set; } = true;
        /// <summary>Walking pace the animator should show: ground speed actually covered over the last tick,
        /// 0 while dodging or after a jump too big to be a step (a door, a respawn).</summary>
        public float AnimSpeed { get; private set; }
        /// <summary>Stepping back from her target with her facing held on it (plays the walk in reverse).</summary>
        public bool Backpedaling { get; private set; }

        /// <summary>0..1 of her silhouette a cover prop blocks from the nearest living threat; 0 outside battle
        /// or with nothing alive to hide from.</summary>
        public float CoverFraction
        {
            get
            {
                var nearest = Threats?.NearestLivingEnemy(Position);
                return nearest != null ? World.CoverExposure(Position, nearest.AimPoint) : 0f;
            }
        }

        /// <summary>True once enough of her is hidden to matter (HUD shield icon, idle facing).</summary>
        public bool InCover => CoverFraction > 0.05f;

        public event Action DodgeStarted;
        public event Action ShootdodgeStarted;
        public event Action ShootdodgeLanded;

        /// <summary>Menus, cutscenes and interactions take a lock; movement resumes when all are released.</summary>
        public void Lock() => _locks++;
        public void Unlock() => _locks = Math.Max(0, _locks - 1);

        public void Root(float seconds) => _rootedUntil = Math.Max(_rootedUntil, Now + seconds);

        /// <summary>Ends an in-progress root immediately (a successful active reload cuts its own lock short).</summary>
        public void Unroot() => _rootedUntil = Now;

        /// <summary>Invulnerability frames outside a dodge (the katana rush).</summary>
        public void GrantInvulnerability(float seconds) => _invulnerableUntil = Math.Max(_invulnerableUntil, Now + seconds);

        public void FaceTowards(Vec3 point)
        {
            var d = (point - Position).Flat;
            if (d.SqrMagnitude > 0.0001f) Yaw = SimMath.YawOf(d);
        }

        /// <summary>Places the body without physics (doors, respawn, the rush's dash).</summary>
        public void Teleport(Vec3 position, float yaw)
        {
            _physics.Teleport(position);
            Yaw = yaw;
            _velocity = Vec3.Zero;
        }

        /// <summary>Takes up the facing the presentation was placed with (scene start, editor placement).</summary>
        public void AdoptYaw(float yaw) => _yaw = yaw;

        /// <param name="dt">Scaled tick seconds (0 while the world is paused: nothing moves).</param>
        /// <param name="realDt">The tick's real seconds (the shootdodge leap ignores bullet time).</param>
        public void Tick(in TickInput input, float dt, float realDt)
        {
            if (dt <= 0f) return;
            var stickHeld = input.MoveX * input.MoveX + input.MoveY * input.MoveY > 0.0001f;
            _basis.Tick(dt, input.CameraYaw, stickHeld);
            var (wx, wz) = _basis.ToWorld(input.MoveX, input.MoveY);
            var wish = new Vec3(wx, 0f, wz);
            CurrentWish = wish;

            if (input.CancelPressed && DodgeEnabled && CanAct && _locks == 0 && Now >= _dodgeReadyAt && !IsDodging)
                StartDodge(wish);

            if (IsShootdodging)
            {
                // Real time, not scaled: the dive itself stays at normal speed while bullet time slows
                // everything else, the same way Max's own leap never slows down around him.
                _shootdodgeVerticalSpeed -= BattleConfig.ShootdodgeGravity * realDt;
                var step = (_shootdodgeDirection * BattleConfig.ShootdodgeSpeed + Vec3.Up * _shootdodgeVerticalSpeed) * realDt;
                _physics.Move(step);
                if (_physics.IsGrounded && _shootdodgeVerticalSpeed <= 0f) EndShootdodge();
                _animLastPosition = Position;
                return;
            }

            Vec3 horizontal;
            if (IsDodging)
            {
                var k = SimMath.Clamp01((_dodgeEnds - Now) / BattleConfig.DodgeSeconds);
                horizontal = _dodgeDirection * (5f + 6.5f * k);
                Backpedaling = false;
            }
            else
            {
                var target = CanAct ? wish * BattleConfig.PlayerRunSpeed : Vec3.Zero;
                if (CanAct) target *= ApproachSlowdown(wish);
                var rate = target.SqrMagnitude > _velocity.SqrMagnitude ? _settings.Acceleration : _settings.Deceleration;
                _velocity = Vec3.MoveTowards(_velocity, target, rate * dt);
                horizontal = _velocity;
                if (CanAct && wish.SqrMagnitude > 0.0001f)
                {
                    if (TryBackpedal(wish, dt))
                        Backpedaling = true;
                    else
                    {
                        Backpedaling = false;
                        Yaw = SimMath.TurnToward(_yaw, SimMath.YawOf(wish), 1f - SimMath.Exp(-_settings.TurnSpeed * dt));
                    }
                }
                else
                {
                    Backpedaling = false;
                    if (CanAct && InCover) LeanTowardNearestThreat(dt);
                }
            }

            _verticalSpeed = _physics.IsGrounded ? -2f : _verticalSpeed - 20f * dt;
            _physics.Move((horizontal + Vec3.Up * _verticalSpeed) * dt);

            // Driven by actual displacement, not just the stick-fed velocity: any real movement across the
            // ground plane plays the walk/jog blend, including a script moving her with Teleport(), not only
            // input. A single huge jump (a door, a respawn) is excluded so it snaps to idle instead of a sprint.
            var moved = (Position - _animLastPosition).Flat;
            var xzSpeed = moved.Magnitude / dt;
            var plausible = xzSpeed <= BattleConfig.PlayerRunSpeed * 2f;
            AnimSpeed = IsDodging || !plausible ? 0f : xzSpeed;
            _animLastPosition = Position;
        }

        /// <summary>Stepping away from whoever she just attacked, within the backpedal window: holds her
        /// facing on the target (or the nearest threat) instead of turning into the retreat direction.</summary>
        private bool TryBackpedal(Vec3 wish, float dt)
        {
            var threats = Threats;
            if (threats == null || !threats.InBattle || threats.SecondsSinceLastAttack > BattleConfig.BackpedalWindowSeconds) return false;
            var target = threats.LastAttackTarget;
            if (target == null || !target.IsAlive) target = threats.NearestLivingEnemy(Position);
            if (target == null) return false;
            var toTarget = (target.Position - Position).Flat;
            if (toTarget.SqrMagnitude < 0.0001f) return false;
            // Only while actually moving away from it, not side-stepping or closing in.
            if (Vec3.Dot(wish.Normalized, toTarget.Normalized) > -0.3f) return false;
            Yaw = SimMath.TurnToward(_yaw, SimMath.YawOf(toTarget), 1f - SimMath.Exp(-_settings.TurnSpeed * dt));
            return true;
        }

        /// <summary>1 with nothing ahead, tapering toward the minimum factor as she closes on solid geometry
        /// she's actually heading into, so she settles against walls instead of ramming them.</summary>
        private float ApproachSlowdown(Vec3 wish)
        {
            if (wish.SqrMagnitude < 0.0001f) return 1f;
            if (!_physics.TryObstacleAhead(Position, wish, out var dist)) return 1f;
            var k = SimMath.Clamp01(dist / _physics.ObstacleRadius);
            return SimMath.Lerp(_settings.ObstacleMinSpeedFactor, 1f, k);
        }

        /// <summary>Sheltering and otherwise idle: turn to face the nearest threat, as if peeking from cover.</summary>
        private void LeanTowardNearestThreat(float dt)
        {
            var nearest = Threats?.NearestLivingEnemy(Position);
            if (nearest == null) return;
            var d = (nearest.Position - Position).Flat;
            if (d.SqrMagnitude < 0.0001f) return;
            Yaw = SimMath.TurnToward(_yaw, SimMath.YawOf(d), 1f - SimMath.Exp(-_settings.TurnSpeed * dt));
        }

        private void StartDodge(Vec3 wish)
        {
            _dodgeDirection = wish.SqrMagnitude > 0.01f ? wish.Normalized : -Forward;
            _dodgeStarted = Now;
            _dodgeEnds = Now + BattleConfig.DodgeSeconds;
            _invulnerableUntil = Now + BattleConfig.DodgeInvulnerableSeconds;
            _dodgeReadyAt = Now + BattleConfig.DodgeCooldownSeconds;
            _velocity = Vec3.Zero;
            DodgeStarted?.Invoke();
        }

        /// <summary>Max Payne shootdodge: leaps along <paramref name="wish"/> (or her facing, with no stick
        /// input), untouchable and free to fire for as long as she's airborne. The battle gates when this is
        /// allowed and owns bullet time and the landing damage; this owns only the leap.</summary>
        public bool TryStartShootdodge(Vec3 wish)
        {
            if (IsShootdodging || IsDodging || !CanAct || !ShootdodgeReady) return false;
            IsShootdodging = true;
            _shootdodgeDirection = wish.SqrMagnitude > 0.01f ? wish.Normalized : Forward;
            _shootdodgeVerticalSpeed = BattleConfig.ShootdodgeRiseSpeed;
            _velocity = Vec3.Zero;
            Yaw = SimMath.YawOf(_shootdodgeDirection);
            ShootdodgeStarted?.Invoke();
            return true;
        }

        private void EndShootdodge()
        {
            IsShootdodging = false;
            _shootdodgeReadyAt = Now + BattleConfig.ShootdodgeCooldownSeconds;
            Root(BattleConfig.ShootdodgeLandStunSeconds);
            ShootdodgeLanded?.Invoke();
        }

        /// <summary>A line of numbers that changes whenever the body's state does (replay comparison).</summary>
        public string Fingerprint() => FormattableString.Invariant(
            $"{Position} yaw {_yaw:R} vel {_velocity} vy {_verticalSpeed:R} dodge {_dodgeStarted:R}/{_dodgeEnds:R} sd {IsShootdodging} {_shootdodgeVerticalSpeed:R} root {_rootedUntil:R} inv {_invulnerableUntil:R} locks {_locks}");
    }
}

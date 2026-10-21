using UnityEngine;
using Unity.Eve.Battle;
using Unity.Eve.Cameras;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Camera;
using Unity.Eve.Input;

namespace Unity.Eve.Player
{
    /// <summary>
    /// Camera-relative movement with a direction latch across fixed-camera cuts, plus a dodge leap
    /// with invulnerability frames. Runs on scaled time, so it freezes with the world-pause.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _runSpeed = BattleConfig.PlayerRunSpeed;
        [SerializeField] private float _turnSpeed = 12f;
        [SerializeField] private float _acceleration = 26f;
        [SerializeField] private float _deceleration = 34f;
        /// <summary>Minimum fraction of run speed she's eased down to right up against something in her path.</summary>
        [SerializeField] private float _obstacleMinSpeedFactor = 0.3f;
        [SerializeField] private Animator _animator;

        private readonly MovementBasis _basis = new MovementBasis();
        private CharacterController _body;
        private ObstacleAwareness _awareness;
        private Vector3 _velocity;
        private float _verticalSpeed;
        private float _rootedUntil;
        private float _dodgeEnds = -1f;
        private float _dodgeStarted = -10f;
        private float _invulnerableUntil;
        private float _dodgeReadyAt;
        private Vector3 _dodgeDirection;
        private int _locks;
        private int _suppressDodgeFrame = -1;
        private Vector3 _animLastPosition;
        private bool _backpedaling;
        private bool _shootdodging;
        private float _shootdodgeVerticalSpeed;
        private Vector3 _shootdodgeDirection;
        private float _shootdodgeReadyAt = -999f;
        private Vector3 _currentWish;

        public Animator Animator => _animator;
        public bool IsDodging => Time.time < _dodgeEnds;
        /// <summary>Max Payne shootdodge: airborne, committed to a straight leap, untouchable, firing.</summary>
        public bool IsShootdodging => _shootdodging;
        public bool ShootdodgeReady => Time.time >= _shootdodgeReadyAt;
        /// <summary>This frame's camera-relative movement direction (what the stick is asking for right
        /// now), for BattleDirector to hand back as the leap direction when it starts a shootdodge.</summary>
        public Vector3 CurrentWish => _currentWish;
        public bool IsInvulnerable => Time.time < _invulnerableUntil || _shootdodging;
        /// <summary>Scaled seconds since the most recent dodge started (for perfect-dodge timing).</summary>
        public float SecondsSinceDodge => Time.time - _dodgeStarted;
        public bool CanAct => _locks == 0 && Time.time >= _rootedUntil && !IsDodging && !_shootdodging && (_character == null || !_character.IsDead);
        /// <summary>0..1 of Kat's silhouette a cover prop blocks from the nearest living threat; 0 outside
        /// battle or with nothing alive to hide from.</summary>
        public float CoverFraction
        {
            get
            {
                var nearest = NearestEnemy();
                return nearest != null ? Cover.Exposure(transform.position, nearest.AimPoint) : 0f;
            }
        }
        /// <summary>True once enough of her is hidden to matter (HUD shield icon, idle facing).</summary>
        public bool InCover => CoverFraction > 0.05f;
        /// <summary>True when the dodge button should be honoured this frame.</summary>
        public bool DodgeEnabled { get; set; } = true;
        public Vector3 Velocity => _velocity;

        public void Configure(Animator animator) => _animator = animator;

        private PlayerCharacter _character;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _character = GetComponent<PlayerCharacter>();
            _awareness = GetComponent<ObstacleAwareness>();
            _animLastPosition = transform.position;
        }

        /// <summary>Menus, cutscenes and interactions take a lock; movement resumes when all are released.</summary>
        public void Lock() => _locks++;
        public void Unlock() => _locks = Mathf.Max(0, _locks - 1);

        /// <summary>The cancel button that closed a menu this frame must not also trigger a dodge.</summary>
        public void SuppressDodgeThisFrame() => _suppressDodgeFrame = Time.frameCount;

        public void Root(float seconds) => _rootedUntil = Mathf.Max(_rootedUntil, Time.time + seconds);

        /// <summary>Ends an in-progress root immediately (a successful active reload cuts its own lock short).</summary>
        public void Unroot() => _rootedUntil = Time.time;

        /// <summary>Invulnerability frames outside a dodge (the katana rush).</summary>
        public void GrantInvulnerability(float seconds) => _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        public void FaceTowards(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>Moves the controller without physics (doors, respawn).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _body.enabled = true;
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            var input = GameInput.Instance;
            var stick = input != null ? input.Move : Vector2.zero;

            var cameraYaw = CameraDirector.Instance != null ? CameraDirector.Instance.Yaw : 0f;
            _basis.Tick(dt, cameraYaw, stick.sqrMagnitude > 0.0001f);
            var (wx, wz) = _basis.ToWorld(stick.x, stick.y);
            var wish = new Vector3(wx, 0f, wz);
            _currentWish = wish;

            if (input != null && input.CancelPressed && DodgeEnabled && CanAct && _locks == 0 && Time.frameCount != _suppressDodgeFrame && Time.time >= _dodgeReadyAt && !IsDodging)
                StartDodge(wish);

            if (_shootdodging)
            {
                // Real time, not scaled: the dive itself stays at normal speed while bullet time (a
                // WorldTime scale the battle director applies for its duration) slows everything else,
                // the same way Max's own leap never slows down around him.
                var udt = Time.unscaledDeltaTime;
                _shootdodgeVerticalSpeed -= BattleConfig.ShootdodgeGravity * udt;
                var step = (_shootdodgeDirection * BattleConfig.ShootdodgeSpeed + Vector3.up * _shootdodgeVerticalSpeed) * udt;
                _body.Move(step);
                if (_body.isGrounded && _shootdodgeVerticalSpeed <= 0f) EndShootdodge();
                _animLastPosition = transform.position;
                return;
            }

            Vector3 horizontal;
            if (IsDodging)
            {
                var k = Mathf.Clamp01((_dodgeEnds - Time.time) / BattleConfig.DodgeSeconds);
                horizontal = _dodgeDirection * (5f + 6.5f * k);
                _backpedaling = false;
            }
            else
            {
                var target = CanAct ? wish * _runSpeed : Vector3.zero;
                if (CanAct) target *= ApproachSlowdown(wish);
                var rate = target.sqrMagnitude > _velocity.sqrMagnitude ? _acceleration : _deceleration;
                _velocity = Vector3.MoveTowards(_velocity, target, rate * dt);
                horizontal = _velocity;
                if (CanAct && wish.sqrMagnitude > 0.0001f)
                {
                    if (TryBackpedal(wish, dt))
                        _backpedaling = true;
                    else
                    {
                        _backpedaling = false;
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), 1f - Mathf.Exp(-_turnSpeed * dt));
                    }
                }
                else
                {
                    _backpedaling = false;
                    if (CanAct && InCover) LeanTowardNearestThreat(dt);
                }
            }

            _verticalSpeed = _body.isGrounded ? -2f : _verticalSpeed - 20f * dt;
            _body.Move((horizontal + Vector3.up * _verticalSpeed) * dt);

            // Driven by actual displacement, not just the stick-fed _velocity: any real movement across
            // the ground plane plays the walk/jog blend, including a script moving her with Teleport()
            // (the autopilot's retreat), not only input. A single huge jump (a door, a respawn, a big
            // scripted hop across the map) is excluded so it snaps to idle instead of flashing a sprint.
            var moved = transform.position - _animLastPosition;
            moved.y = 0f;
            var xzSpeed = moved.magnitude / dt;
            var plausible = xzSpeed <= BattleConfig.PlayerRunSpeed * 2f;
            if (_animator != null)
            {
                _animator.SetFloat(AnimParams.Speed, IsDodging || !plausible ? 0f : xzSpeed, 0.08f, dt);
                _animator.SetBool(AnimParams.Backpedal, _backpedaling);
            }
            _animLastPosition = transform.position;
        }

        /// <summary>Stepping away from whoever she just attacked, within the backpedal window: holds her
        /// facing on the target (or the nearest threat) and lets the Backpedal animator state play the
        /// walk cycle in reverse, instead of turning into the retreat direction like ordinary movement.</summary>
        private bool TryBackpedal(Vector3 wish, float dt)
        {
            var battle = BattleDirector.Instance;
            if (battle == null || !battle.InBattle || battle.SecondsSinceLastAttack > BattleConfig.BackpedalWindowSeconds) return false;
            var target = battle.LastAttackTarget;
            if (target == null || !target.IsAlive) target = NearestEnemy();
            if (target == null) return false;
            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return false;
            // Only while actually moving away from her, not side-stepping or closing in.
            if (Vector3.Dot(wish.normalized, toTarget.normalized) > -0.3f) return false;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), 1f - Mathf.Exp(-_turnSpeed * dt));
            return true;
        }

        /// <summary>1 with nothing ahead, tapering toward <see cref="_obstacleMinSpeedFactor"/> as she
        /// closes on solid geometry she's actually heading into (the outer warning collider). Settling
        /// into furniture and walls at a crawl instead of ramming them at full speed is what stops the
        /// CharacterController's collide-and-slide from jittering on contact.</summary>
        private float ApproachSlowdown(Vector3 wish)
        {
            if (_awareness == null || wish.sqrMagnitude < 0.0001f) return 1f;
            if (!_awareness.TryGetNearest(transform.position, wish, out _, out var dist)) return 1f;
            var k = Mathf.Clamp01(dist / _awareness.Radius);
            return Mathf.Lerp(_obstacleMinSpeedFactor, 1f, k);
        }

        /// <summary>Sheltering and otherwise idle: turn to face the nearest threat, as if peeking from behind cover.</summary>
        private void LeanTowardNearestThreat(float dt)
        {
            var nearest = NearestEnemy();
            if (nearest == null) return;
            var d = nearest.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-_turnSpeed * dt));
        }

        private Enemies.EnemyActor NearestEnemy()
        {
            var battle = BattleDirector.Instance;
            if (battle == null || !battle.InBattle) return null;
            Enemies.EnemyActor nearest = null;
            var bestSqr = float.MaxValue;
            foreach (var e in battle.Enemies)
            {
                if (!e.IsAlive) continue;
                var sqr = (e.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = e;
                }
            }
            return nearest;
        }

        private void StartDodge(Vector3 wish)
        {
            _dodgeDirection = wish.sqrMagnitude > 0.01f ? wish.normalized : -transform.forward;
            _dodgeStarted = Time.time;
            _dodgeEnds = Time.time + BattleConfig.DodgeSeconds;
            _invulnerableUntil = Time.time + BattleConfig.DodgeInvulnerableSeconds;
            _dodgeReadyAt = Time.time + BattleConfig.DodgeCooldownSeconds;
            _velocity = Vector3.zero;
            if (_animator != null) _animator.SetTrigger(AnimParams.Dodge);
            AudioService.Play(SoundId.Dodge);
        }

        /// <summary>Max Payne shootdodge: leaps in <paramref name="wish"/> (or her facing, with no stick
        /// input), untouchable and free to fire for as long as she's airborne. BattleDirector gates when
        /// this is allowed (battle active, a ranged weapon equipped, off cooldown) and owns the bullet-time
        /// scale and the landing damage; this only owns the leap itself.</summary>
        public bool TryStartShootdodge(Vector3 wish)
        {
            if (_shootdodging || IsDodging || !CanAct || !ShootdodgeReady) return false;
            _shootdodging = true;
            _shootdodgeDirection = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
            _shootdodgeVerticalSpeed = BattleConfig.ShootdodgeRiseSpeed;
            _velocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(_shootdodgeDirection);
            if (_animator != null) _animator.SetTrigger(AnimParams.Dodge);
            AudioService.Play(SoundId.Shootdodge);
            return true;
        }

        private void EndShootdodge()
        {
            _shootdodging = false;
            _shootdodgeReadyAt = Time.time + BattleConfig.ShootdodgeCooldownSeconds;
            Root(BattleConfig.ShootdodgeLandStunSeconds);
            if (_animator != null) _animator.SetTrigger(AnimParams.Hit);
            AudioService.Play(SoundId.HardLand);
        }
    }
}

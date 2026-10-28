using UnityEngine;
using Unity.Eve.Core.Session;
using Unity.Eve.Core.Sim;
using Unity.Eve.Game;

namespace Unity.Eve.Player
{
    /// <summary>
    /// Kat's body in the scene. Her movement, dodge, roots and shootdodge are the Core <see cref="PlayerBody"/>
    /// the authority steps at the fixed tick; this is its collision (the CharacterController, behind
    /// <see cref="IBodyPhysics"/>) and its presentation (animator, sounds). Everything that used to poke the
    /// controller still can, and goes straight to the body.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IBodyPhysics
    {
        [SerializeField] private float _turnSpeed = 12f;
        [SerializeField] private float _acceleration = 26f;
        [SerializeField] private float _deceleration = 34f;
        /// <summary>Minimum fraction of run speed she's eased down to right up against something in her path.</summary>
        [SerializeField] private float _obstacleMinSpeedFactor = 0.3f;
        [SerializeField] private Animator _animator;

        private CharacterController _cc;
        private ObstacleAwareness _awareness;
        private PlayerCharacter _character;
        private GameAuthority _authority;

        /// <summary>Her body as the authority owns it.</summary>
        public PlayerBody Body { get; private set; }

        public Animator Animator => _animator;
        public bool IsDodging => Body.IsDodging;
        /// <summary>Max Payne shootdodge: airborne, committed to a straight leap, untouchable, firing.</summary>
        public bool IsShootdodging => Body.IsShootdodging;
        public bool ShootdodgeReady => Body.ShootdodgeReady;
        public Vector3 CurrentWish => Body.CurrentWish.ToUnity();
        public bool IsInvulnerable => Body.IsInvulnerable;
        /// <summary>Scaled seconds since the most recent dodge started (for perfect-dodge timing).</summary>
        public float SecondsSinceDodge => Body.SecondsSinceDodge;
        public bool CanAct => Body.CanAct;
        /// <summary>0..1 of Kat's silhouette a cover prop blocks from the nearest living threat.</summary>
        public float CoverFraction => Body.CoverFraction;
        public bool InCover => Body.InCover;
        public bool DodgeEnabled
        {
            get => Body.DodgeEnabled;
            set => Body.DodgeEnabled = value;
        }
        public Vector3 Velocity => Body.Velocity.ToUnity();

        public void Configure(Animator animator) => _animator = animator;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _character = GetComponent<PlayerCharacter>();
            _awareness = GetComponent<ObstacleAwareness>();
            _authority = SimHost.For(gameObject.scene);
            Body = new PlayerBody(_character.State, this, _authority.Clock, new BodySettings
            {
                TurnSpeed = _turnSpeed,
                Acceleration = _acceleration,
                Deceleration = _deceleration,
                ObstacleMinSpeedFactor = _obstacleMinSpeedFactor,
            });
            Body.AdoptYaw(SimConvert.YawOf(transform.rotation));
            Body.DodgeStarted += () =>
            {
                if (_animator != null) _animator.SetTrigger(AnimParams.Dodge);
                AudioService.Play(SoundId.Dodge);
            };
            Body.ShootdodgeStarted += () =>
            {
                if (_animator != null) _animator.SetTrigger(AnimParams.Dodge);
                AudioService.Play(SoundId.Shootdodge);
            };
            Body.ShootdodgeLanded += () =>
            {
                if (_animator != null) _animator.SetTrigger(AnimParams.Hit);
                AudioService.Play(SoundId.HardLand);
            };
            _authority.Register(Body);
        }

        private void OnDestroy() => _authority?.Unregister(Body);

        /// <summary>Menus, cutscenes and interactions take a lock; movement resumes when all are released.</summary>
        public void Lock() => Body.Lock();
        public void Unlock() => Body.Unlock();

        /// <summary>The cancel button that closed a menu must not also trigger a dodge.</summary>
        public void SuppressDodgeThisFrame() => _character.Controls?.Suppress(IntentButtons.Cancel);

        public void Root(float seconds) => Body.Root(seconds);

        /// <summary>Ends an in-progress root immediately.</summary>
        public void Unroot() => Body.Unroot();

        /// <summary>Invulnerability frames outside a dodge.</summary>
        public void GrantInvulnerability(float seconds) => Body.GrantInvulnerability(seconds);

        public void FaceTowards(Vector3 point) => Body.FaceTowards(point.ToSim());

        /// <summary>Moves her without physics (doors, respawn, the self-play's scripted steps), through the
        /// command pipeline when a session is running so a replay puts her in the same place.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            var yaw = SimConvert.YawOf(rotation);
            var session = GameSession.Instance != null ? GameSession.Instance.Session : null;
            if (session != null && session.Submit(new TeleportCommand(_character.Seat, position.x, position.y, position.z, yaw))) return;
            Body.Teleport(position.ToSim(), yaw);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f || _animator == null || Body.IsShootdodging) return;
            _animator.SetFloat(AnimParams.Speed, Body.AnimSpeed, 0.08f, dt);
            _animator.SetBool(AnimParams.Backpedal, Body.Backpedaling);
        }

        // ------------------------------------------------------------------ IBodyPhysics

        Vec3 IBodyPhysics.Position => transform.position.ToSim();

        void IBodyPhysics.Move(Vec3 motion) => _cc.Move(motion.ToUnity());

        bool IBodyPhysics.IsGrounded => _cc.isGrounded;

        void IBodyPhysics.Teleport(Vec3 position)
        {
            _cc.enabled = false;
            transform.position = position.ToUnity();
            _cc.enabled = true;
        }

        void IBodyPhysics.SetYaw(float yaw) => transform.rotation = SimConvert.YawRotation(yaw);

        bool IBodyPhysics.TryObstacleAhead(Vec3 from, Vec3 direction, out float distance)
        {
            distance = 0f;
            return _awareness != null && _awareness.TryGetNearest(from.ToUnity(), direction.ToUnity(), out _, out distance);
        }

        float IBodyPhysics.ObstacleRadius => _awareness != null ? _awareness.Radius : 1f;
    }
}

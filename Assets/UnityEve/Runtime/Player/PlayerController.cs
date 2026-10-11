using UnityEngine;
using UnityEve.Cameras;
using UnityEve.Core.Battle;
using UnityEve.Core.Camera;
using UnityEve.Input;

namespace UnityEve.Player
{
    /// <summary>
    /// Camera-relative movement with a direction latch across fixed-camera cuts, plus a dodge leap
    /// with invulnerability frames. Runs on scaled time, so it freezes with the world-pause.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _runSpeed = 4f;
        [SerializeField] private float _turnSpeed = 12f;
        [SerializeField] private float _acceleration = 26f;
        [SerializeField] private float _deceleration = 34f;
        [SerializeField] private Animator _animator;

        private readonly MovementBasis _basis = new MovementBasis();
        private CharacterController _body;
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

        public Animator Animator => _animator;
        public bool IsDodging => Time.time < _dodgeEnds;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        /// <summary>Scaled seconds since the most recent dodge started (for perfect-dodge timing).</summary>
        public float SecondsSinceDodge => Time.time - _dodgeStarted;
        public bool CanAct => _locks == 0 && Time.time >= _rootedUntil && !IsDodging && (_character == null || !_character.IsDead);
        /// <summary>True when the dodge button should be honoured this frame.</summary>
        public bool DodgeEnabled { get; set; } = true;
        public Vector3 Velocity => _velocity;

        public void Configure(Animator animator) => _animator = animator;

        private PlayerCharacter _character;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _character = GetComponent<PlayerCharacter>();
        }

        /// <summary>Menus, cutscenes and interactions take a lock; movement resumes when all are released.</summary>
        public void Lock() => _locks++;
        public void Unlock() => _locks = Mathf.Max(0, _locks - 1);

        /// <summary>The cancel button that closed a menu this frame must not also trigger a dodge.</summary>
        public void SuppressDodgeThisFrame() => _suppressDodgeFrame = Time.frameCount;

        public void Root(float seconds) => _rootedUntil = Mathf.Max(_rootedUntil, Time.time + seconds);

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

            if (input != null && input.CancelPressed && DodgeEnabled && CanAct && _locks == 0 && Time.frameCount != _suppressDodgeFrame && Time.time >= _dodgeReadyAt && !IsDodging)
                StartDodge(wish);

            Vector3 horizontal;
            if (IsDodging)
            {
                var k = Mathf.Clamp01((_dodgeEnds - Time.time) / BattleConfig.DodgeSeconds);
                horizontal = _dodgeDirection * (5f + 6.5f * k);
            }
            else
            {
                var target = CanAct ? wish * _runSpeed : Vector3.zero;
                var rate = target.sqrMagnitude > _velocity.sqrMagnitude ? _acceleration : _deceleration;
                _velocity = Vector3.MoveTowards(_velocity, target, rate * dt);
                horizontal = _velocity;
                if (CanAct && wish.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), 1f - Mathf.Exp(-_turnSpeed * dt));
            }

            _verticalSpeed = _body.isGrounded ? -2f : _verticalSpeed - 20f * dt;
            _body.Move((horizontal + Vector3.up * _verticalSpeed) * dt);

            if (_animator != null) _animator.SetFloat(AnimParams.Speed, IsDodging ? 0f : _velocity.magnitude, 0.08f, dt);
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
    }
}

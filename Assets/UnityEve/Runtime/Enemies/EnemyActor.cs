using UnityEngine;
using Unity.Eve.Battle;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Sim;
using Unity.Eve.Data;
using Unity.Eve.Game;
using Unity.Eve.UI;

namespace Unity.Eve.Enemies
{
    /// <summary>
    /// A creature in battle as the scene shows it. Its state and behaviour (position, facing, health, its
    /// <see cref="EnemyMind"/> and attack cycle) are the Core <see cref="EnemyState"/> the battle authority
    /// steps at the fixed tick; this places the model between the last two ticks, drives its animator or
    /// procedural rig, and plays its telegraph glow, sounds and death.
    /// </summary>
    [DefaultExecutionOrder(100)] // presents after GameSession's LateUpdate has stepped the authority
    public sealed class EnemyActor : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private EnemyDefinition _definition;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _bodyRadius = 0.35f;
        [SerializeField] private float _aimHeight = 0.4f;

        private Renderer[] _renderers;
        private ProceduralCreature _procedural;
        private MaterialPropertyBlock _block;
        private SimClock _clock;
        private Transform _player;
        private float _hurtFlashUntil;
        private float _deathTime = -1f;
        private Vector3 _baseScale;

        public EnemyDefinition Definition => _definition;
        public EnemyStats Stats => _definition.Stats;
        /// <summary>Its rules and state, owned by the battle authority (null until spawned into a battle).</summary>
        public EnemyState State { get; private set; }
        public Health Health => State?.Health;
        public bool IsAlive => State != null && State.IsAlive;
        public bool IsCommitted => State != null && State.IsCommitted;
        public EnemyIntent Intent => State != null ? State.Intent : EnemyIntent.Think;
        public bool Buffed => State != null && State.Buffed;
        public bool RearingUp => State != null && State.RearingUp;
        /// <summary>Seconds until this creature's committed strike lands; +infinity if it isn't attacking.</summary>
        public float SecondsUntilStrike => State != null ? State.SecondsUntilStrike : float.PositiveInfinity;
        /// <summary>Strike damage multiplier (an enraged creature hits harder).</summary>
        public float DamageMultiplier => State != null ? State.DamageMultiplier : 1f;
        public float BodyRadius => _bodyRadius;
        /// <summary>Height of its weak point above its feet, before any hover.</summary>
        public float AimHeight => _aimHeight;
        public Vector3 AimPoint => State != null ? State.AimPoint.ToUnity() : transform.position + Vector3.up * (_aimHeight + Stats.HoverHeight);
        public bool Removed { get; private set; }

        public void Configure(EnemyDefinition definition, Animator animator, float bodyRadius, float aimHeight)
        {
            _definition = definition;
            _animator = animator;
            _bodyRadius = bodyRadius;
            _aimHeight = aimHeight;
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _procedural = GetComponentInChildren<ProceduralCreature>();
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
        }

        /// <summary>Called by the battle director when the authority spawns this creature.</summary>
        public void Bind(EnemyState state, SimClock clock, Transform player)
        {
            State = state;
            _clock = clock;
            _player = player;
            state.WindupStarted += _ =>
            {
                if (_animator != null) _animator.SetTrigger(AnimParams.Attack);
                _procedural?.Attack();
                AudioService.Play(SoundId.EnemyTelegraph, 0.7f);
            };
            state.StrikeLanded += _ =>
            {
                if (!Stats.Ranged) return;
                var color = Stats.LeavesPuddle ? new Color(0.6f, 1f, 0.3f) : new Color(1f, 0.85f, 0.2f);
                if (_player != null) BattleFx.Instance?.Tracer(AimPoint, _player.position + Vector3.up * 0.9f, color);
                AudioService.Play(Stats.LeavesPuddle ? SoundId.EnemySpit : SoundId.Stinger, 0.7f);
            };
            state.IntentChosen += (_, intent) =>
            {
                if (intent == EnemyIntent.Buff) AudioService.Play(SoundId.EnemyBuffCharge, 0.6f);
                if (intent == EnemyIntent.Charge) AudioService.Play(SoundId.EnemyCharge, 0.8f);
            };
            state.BuffApplied += _ =>
            {
                AudioService.Play(SoundId.EnemyBuff);
                GameHud.Instance?.Popup(AimPoint + Vector3.up * 0.6f, "ENRAGED", new Color(1f, 0.55f, 0.2f));
            };
            state.Hit += (_, __) => _hurtFlashUntil = Time.unscaledTime + 0.18f;
            state.Died += _ =>
            {
                _deathTime = Time.time;
                if (_animator != null) _animator.SetTrigger(AnimParams.Die);
                _procedural?.Die();
                AudioService.Play(SoundId.EnemyDeath);
            };
            transform.SetPositionAndRotation(state.Position.ToUnity(), SimConvert.YawRotation(state.Yaw));
            transform.localScale = _baseScale * 0.01f;
        }

        private void LateUpdate()
        {
            if (State == null) return;
            var dt = Time.deltaTime;

            // Materialize: grow in over 0.4 s of game time (it pauses with the world).
            if (_deathTime < 0f) transform.localScale = _baseScale * Mathf.Max(0.01f, State.Grown);

            if (!State.IsAlive)
            {
                UpdateCorpse();
                return;
            }

            // Between the last two ticks, so it moves smoothly at any frame rate.
            var alpha = _clock != null ? _clock.Alpha : 1f;
            var from = Quaternion.Euler(0f, State.PreviousYaw * Mathf.Rad2Deg, 0f);
            var to = Quaternion.Euler(0f, State.Yaw * Mathf.Rad2Deg, 0f);
            transform.SetPositionAndRotation(Vector3.Lerp(State.PreviousPosition.ToUnity(), State.Position.ToUnity(), alpha),
                Quaternion.Slerp(from, to, alpha));

            var stunned = State.Stunned;
            if (_animator != null)
            {
                _animator.SetFloat(AnimParams.Speed, State.Speed, 0.08f, Mathf.Max(dt, 1e-4f));
                _animator.speed = stunned ? 0.2f : State.Tempo;
            }
            if (_procedural != null)
            {
                _procedural.SetSpeed(State.Speed);
                _procedural.Playback = stunned ? 0.2f : State.Tempo;
            }

            // Rearing up before a charge: a quick crouch-and-swell the player can read.
            if (State.RearingUp && _deathTime < 0f)
            {
                var k = Mathf.Sin(Time.time * 18f) * 0.06f;
                transform.localScale = new Vector3(_baseScale.x * (1.08f + k), _baseScale.y * (0.86f - k), _baseScale.z * (1.08f + k));
            }

            UpdateTint(stunned);
        }

        private void UpdateTint(bool stunned)
        {
            Color c;
            if (Time.unscaledTime < _hurtFlashUntil && Mathf.Repeat(Time.unscaledTime * 30f, 1f) < 0.5f)
                c = new Color(0.55f, 0.12f, 0.12f);
            else if (State.Phase == AttackPhase.Windup)
            {
                var heat = State.WindupProgress;
                c = new Color(0.1f + heat * 0.9f, 0.02f + heat * 0.15f, 0.02f);
            }
            else if (stunned)
                c = new Color(0.2f, 0.2f, 0.35f);
            else if (State.RearingUp)
                c = new Color(0.5f, 0.12f, 0.02f) * (0.6f + 0.4f * Mathf.Sin(Time.time * 40f));
            else if (State.Intent == EnemyIntent.Buff)
            {
                // Charging up: a throbbing violet that builds toward the release.
                var k = State.BuffCastProgress;
                c = new Color(0.35f, 0.05f, 0.5f) * (0.3f + 0.7f * k) * (0.75f + 0.25f * Mathf.Sin(Time.time * 30f));
            }
            else if (State.Buffed)
                c = new Color(0.28f, 0.08f, 0f) * (0.7f + 0.3f * Mathf.Sin(Time.time * 6f));
            else
                c = Color.black;

            foreach (var r in _renderers)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionColor, c);
                r.SetPropertyBlock(_block);
            }
        }

        private void UpdateCorpse()
        {
            var t = Time.time - _deathTime;
            if (t < 1.1f)
            {
                // Still where the rules last put it (a killing blow's knockback carries the body).
                transform.position = State.Position.ToUnity();
                return;
            }
            transform.position += Vector3.down * (0.5f * Time.deltaTime);
            transform.localScale = _baseScale * Mathf.Max(0.05f, 1f - 0.4f * (t - 1.1f));
            if (t > 2.6f && !Removed)
            {
                Removed = true;
                gameObject.SetActive(false);
            }
        }
    }
}

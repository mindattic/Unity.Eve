using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEve.Core.Battle;
using UnityEve.Data;

namespace UnityEve.Enemies
{
    /// <summary>
    /// A creature in battle. Real-time behaviour on scaled time: it closes in, circles while the pack's
    /// attacker limit is full, telegraphs (glows) during its windup, lunges at where the player was,
    /// then recovers. Because it runs on scaled time, the world-pause freezes it mid-telegraph.
    /// </summary>
    public sealed class EnemyActor : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private EnemyDefinition _definition;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _bodyRadius = 0.35f;
        [SerializeField] private float _aimHeight = 0.4f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private EnemyAttackCycle _cycle;
        private Transform _target;
        private Func<bool> _mayCommit;
        private float _stunnedUntil;
        private float _slowUntil;
        private float _hurtFlashUntil;
        private float _recentDamage;
        private float _circleSign = 1f;
        private float _deathTime = -1f;
        private float _spawnTime;
        private Vector3 _anchor;
        private Vector3 _baseScale;

        public EnemyDefinition Definition => _definition;
        public EnemyStats Stats => _definition.Stats;
        public Health Health { get; private set; }
        public bool IsAlive => Health != null && !Health.IsDead;
        public bool IsCommitted => _cycle != null && _cycle.IsCommitted;
        public float BodyRadius => _bodyRadius;
        public Vector3 AimPoint => transform.position + Vector3.up * (_aimHeight + Stats.HoverHeight);
        public bool Removed { get; private set; }

        /// <summary>Raised when a strike lands; the battle director resolves hit/whiff/evade.</summary>
        public event Action<EnemyActor> Struck;
        public event Action<EnemyActor> Died;

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
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
        }

        /// <summary>Called by the encounter when the creature materializes.</summary>
        public void Begin(Transform target, Func<bool> mayCommit, float initialCooldown)
        {
            Health = new Health(Stats.MaxHp);
            _target = target;
            _mayCommit = mayCommit;
            _cycle = new EnemyAttackCycle(Stats, initialCooldown);
            _cycle.WindupStarted += OnWindup;
            _cycle.StrikeLanded += () => Struck?.Invoke(this);
            _circleSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _spawnTime = Time.time;
            transform.localScale = _baseScale * 0.01f;
        }

        private void OnWindup()
        {
            _anchor = _target.position;
            if (_animator != null) _animator.SetTrigger(AnimParams.Attack);
            AudioService.Play(SoundId.EnemyTelegraph, 0.7f);
        }

        public void ApplyStun(float seconds)
        {
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + seconds);
            _cycle?.Interrupt();
        }

        public void ApplySlow(float seconds) => _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);

        public void Knockback(Vector3 from, float metres)
        {
            var d = transform.position - from;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.position += d.normalized * metres;
        }

        /// <returns>Damage dealt.</returns>
        public int TakeHit(int amount)
        {
            if (!IsAlive) return 0;
            var dealt = Health.Damage(amount);
            _hurtFlashUntil = Time.unscaledTime + 0.18f;
            _recentDamage += dealt;
            if (_recentDamage >= Mathf.Max(20f, Stats.MaxHp * 0.35f))
            {
                _recentDamage = 0f;
                ApplyStun(1.8f);
            }
            if (Health.IsDead)
            {
                _deathTime = Time.time;
                if (_animator != null) _animator.SetTrigger(AnimParams.Die);
                AudioService.Play(SoundId.EnemyDeath);
                Died?.Invoke(this);
            }
            return dealt;
        }

        /// <summary>Pushes overlapping pack members apart (called by the director each frame).</summary>
        public static void Separate(IReadOnlyList<EnemyActor> pack, Transform player)
        {
            for (var i = 0; i < pack.Count; i++)
            {
                var a = pack[i];
                if (!a.IsAlive) continue;
                for (var j = i + 1; j < pack.Count; j++)
                {
                    var b = pack[j];
                    if (!b.IsAlive) continue;
                    var d = b.transform.position - a.transform.position;
                    d.y = 0f;
                    var min = a._bodyRadius + b._bodyRadius + 0.14f;
                    var dist = d.magnitude;
                    if (dist >= min || dist < 0.0001f) continue;
                    var push = d / dist * ((min - dist) * 0.5f);
                    a.transform.position -= push;
                    b.transform.position += push;
                }
                var toPlayer = a.transform.position - player.position;
                toPlayer.y = 0f;
                var minP = a._bodyRadius + 0.4f;
                if (toPlayer.magnitude < minP && toPlayer.sqrMagnitude > 0.0001f)
                    a.transform.position = player.position + toPlayer.normalized * minP + Vector3.up * (a.transform.position.y - player.position.y);
            }
        }

        private void Update()
        {
            if (_cycle == null) return;
            var dt = Time.deltaTime;

            // Materialize: grow in over 0.4 s (scaled time, so it pauses with the world).
            var grow = Mathf.Clamp01((Time.time - _spawnTime) / 0.4f);
            if (_deathTime < 0f) transform.localScale = _baseScale * Mathf.Max(0.01f, grow);

            if (!IsAlive)
            {
                UpdateCorpse();
                return;
            }

            var stunned = Time.time < _stunnedUntil;
            var tempo = Time.time < _slowUntil ? 0.5f : 1f;
            var toPlayer = _target.position - transform.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.0001f ? toPlayer / dist : transform.forward;

            if (!stunned && grow >= 1f)
            {
                _cycle.Tick(dt, dist, _mayCommit == null || _mayCommit(), tempo);
                Move(dt, dir, dist, tempo);
            }

            var speed = stunned ? 0f : (_cycle.Phase == AttackPhase.Close ? Stats.MoveSpeed * tempo : 0f);
            if (_animator != null)
            {
                _animator.SetFloat(AnimParams.Speed, speed);
                _animator.speed = stunned ? 0.2f : tempo;
            }

            var y = Stats.HoverHeight > 0f
                ? (_cycle.IsCommitted ? 0.75f : Stats.HoverHeight + Mathf.Sin(Time.time * 2.4f) * 0.1f)
                : 0f;
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-8f * dt));
            transform.position = p;

            UpdateTint(stunned);
        }

        private void Move(float dt, Vector3 dir, float dist, float tempo)
        {
            switch (_cycle.Phase)
            {
                case AttackPhase.Close:
                {
                    Turn(dir, 9f * dt);
                    var speed = Stats.MoveSpeed * tempo;
                    if (dist > Stats.Reach)
                    {
                        transform.position += dir * (speed * dt);
                    }
                    else
                    {
                        // Prowl: circle the player at reach while waiting for a turn to commit.
                        var tangent = new Vector3(-dir.z, 0f, dir.x) * _circleSign;
                        var radial = dir * (dist - Stats.Reach * 0.92f);
                        transform.position += (tangent * 0.85f + radial).normalized * (speed * 0.55f * dt);
                    }
                    break;
                }
                case AttackPhase.Windup:
                    Turn(dir, 2.5f * dt);
                    transform.position -= dir * (Stats.MoveSpeed * 0.18f * dt);
                    break;
                case AttackPhase.Strike:
                {
                    var lunge = _anchor - transform.position;
                    lunge.y = 0f;
                    if (lunge.magnitude > Stats.Reach * 0.5f)
                        transform.position += lunge.normalized * (Stats.MoveSpeed * 3.2f * dt);
                    break;
                }
            }
        }

        private void Turn(Vector3 dir, float t)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(t));
        }

        private void UpdateTint(bool stunned)
        {
            Color c;
            if (Time.unscaledTime < _hurtFlashUntil && Mathf.Repeat(Time.unscaledTime * 30f, 1f) < 0.5f)
                c = new Color(0.55f, 0.12f, 0.12f);
            else if (_cycle.Phase == AttackPhase.Windup)
            {
                var heat = _cycle.WindupProgress;
                c = new Color(0.1f + heat * 0.9f, 0.02f + heat * 0.15f, 0.02f);
            }
            else if (stunned)
                c = new Color(0.2f, 0.2f, 0.35f);
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
            if (t < 1.1f) return;
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

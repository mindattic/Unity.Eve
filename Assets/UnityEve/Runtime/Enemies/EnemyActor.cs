using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using UnityEve.Data;
using UnityEve.UI;

namespace UnityEve.Enemies
{
    /// <summary>
    /// A creature in battle. Real-time behaviour on scaled time. Its <see cref="EnemyMind"/> takes turns with
    /// the pack through attack tokens: with a token it stalks or flanks in, rears up, charges, strikes and
    /// retreats; without one it lurks, circles or powers up on a ring around the player. When it commits it telegraphs (glows) during its windup, lunges at where
    /// the player was, then recovers. Because it runs on scaled time, the world-pause freezes it mid-telegraph.
    /// </summary>
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
        private EnemyAttackCycle _cycle;
        private EnemyMind _mind;
        private Func<int> _packAlive;
        private Bounds _arena;
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
        public EnemyIntent Intent => _mind != null ? _mind.Intent : EnemyIntent.Think;
        public bool Buffed => _mind != null && _mind.Buffed;
        public bool RearingUp => _mind != null && _mind.RearingUp;
        /// <summary>Seconds until this creature's committed strike lands; +infinity if it isn't attacking.</summary>
        public float SecondsUntilStrike => _cycle != null ? _cycle.SecondsUntilStrike : float.PositiveInfinity;
        /// <summary>Strike damage multiplier (an enraged creature hits harder).</summary>
        public float DamageMultiplier => _mind != null ? _mind.DamageMultiplier : 1f;
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
            _procedural = GetComponentInChildren<ProceduralCreature>();
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
        }

        /// <summary>Called by the encounter when the creature materializes.</summary>
        public void Begin(Transform target, Func<bool> mayCommit, float initialCooldown, uint seed, Bounds arena, Func<int> packAlive, AttackTokens tokens)
        {
            _arena = arena;
            _packAlive = packAlive;
            var rng = new Rng(seed);
            // Bosses do not queue for the pack's attack token: the Mother attacks whenever she can reach.
            var queue = tokens != null && !Stats.Boss;
            _mind = new EnemyMind(Stats, rng, rng.Range(0.4f, 1.2f),
                queue ? () => tokens.TryTake(this) : (Func<bool>)null,
                queue ? () => tokens.Release(this) : (Action)null);
            _mind.IntentChosen += OnIntent;
            _mind.BuffApplied += OnBuffApplied;
            Health = new Health(Stats.MaxHp);
            _target = target;
            _mayCommit = mayCommit;
            _cycle = new EnemyAttackCycle(Stats, initialCooldown);
            _cycle.WindupStarted += OnWindup;
            _cycle.StrikeLanded += () =>
            {
                Struck?.Invoke(this);
                _mind.Struck();
            };
            _circleSign = rng.Chance(0.5f) ? -1f : 1f;
            _spawnTime = Time.time;
            transform.localScale = _baseScale * 0.01f;
        }

        private void OnWindup()
        {
            _anchor = _target.position;
            if (_animator != null) _animator.SetTrigger(AnimParams.Attack);
            _procedural?.Attack();
            AudioService.Play(SoundId.EnemyTelegraph, 0.7f);
        }

        private void OnIntent(EnemyIntent intent)
        {
            if (intent == EnemyIntent.Buff) AudioService.Play(SoundId.EnemyBuffCharge, 0.6f);
            if (intent == EnemyIntent.Charge) AudioService.Play(SoundId.EnemyCharge, 0.8f);
        }

        private void OnBuffApplied()
        {
            AudioService.Play(SoundId.EnemyBuff);
            GameHud.Instance?.Popup(AimPoint + Vector3.up * 0.6f, "ENRAGED", new Color(1f, 0.55f, 0.2f));
        }

        public void ApplyStun(float seconds)
        {
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + seconds);
            _cycle?.Interrupt();
            _mind?.Reconsider();
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
                _procedural?.Die();
                AudioService.Play(SoundId.EnemyDeath);
                _mind?.Leave();
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
            var before = transform.position;
            var tempo = Time.time < _slowUntil ? 0.5f : 1f;
            var toPlayer = _target.position - transform.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.0001f ? toPlayer / dist : transform.forward;

            var facing = _target.forward;
            facing.y = 0f;
            var senses = new EnemySenses
            {
                Distance = dist,
                HealthFraction = Health.Fraction,
                PlayerFacing = facing.sqrMagnitude > 0.0001f ? Vector3.Dot(facing.normalized, -dir) : 0f,
                PackAlive = _packAlive != null ? _packAlive() : 1,
            };
            tempo *= _mind.Tempo;

            if (!stunned && grow >= 1f)
            {
                if (_cycle.Phase == AttackPhase.Close) _mind.Tick(dt, senses);
                // Tokens decide whose turn it is; the commit limit stays as a safety net.
                _cycle.Tick(dt, dist, (_mayCommit == null || _mayCommit()) && _mind.MayAttack(senses), tempo);
                Move(dt, dir, dist, tempo, _mind.Steer(senses), facing);
            }

            // Locomotion follows how fast it actually moved: idle when still, walk, run when charging.
            var moved = transform.position - before;
            moved.y = 0f;
            var speed = dt > 0f ? moved.magnitude / dt : 0f;
            if (_animator != null)
            {
                _animator.SetFloat(AnimParams.Speed, speed, 0.08f, Mathf.Max(dt, 1e-4f));
                _animator.speed = stunned ? 0.2f : tempo;
            }
            if (_procedural != null)
            {
                _procedural.SetSpeed(speed);
                _procedural.Playback = stunned ? 0.2f : tempo;
            }

            // Rearing up before a charge: a quick crouch-and-swell the player can read.
            if (_mind.RearingUp && _deathTime < 0f)
            {
                var k = Mathf.Sin(Time.time * 18f) * 0.06f;
                transform.localScale = new Vector3(_baseScale.x * (1.08f + k), _baseScale.y * (0.86f - k), _baseScale.z * (1.08f + k));
            }

            var y = Stats.HoverHeight > 0f
                ? (_cycle.IsCommitted ? 0.75f : Stats.HoverHeight + Mathf.Sin(Time.time * 2.4f) * 0.1f)
                : 0f;
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-8f * dt));
            transform.position = p;

            UpdateTint(stunned);
        }

        private void Move(float dt, Vector3 dir, float dist, float tempo, EnemySteering steering, Vector3 playerFacing)
        {
            switch (_cycle.Phase)
            {
                case AttackPhase.Close:
                {
                    // Always watch the player, even while idling, thinking or powering up.
                    Turn(dir, 9f * dt);
                    if (steering.Still) break;
                    var speed = Stats.MoveSpeed * tempo * steering.Speed;
                    var tangent = new Vector3(-dir.z, 0f, dir.x) * _circleSign;
                    if (steering.TowardBack)
                    {
                        // Go the way round that leads toward her back.
                        tangent = new Vector3(-dir.z, 0f, dir.x);
                        if (Vector3.Dot(tangent, -playerFacing) < 0f) tangent = -tangent;
                    }
                    // Radial error: positive means too far (move in), negative means too close (back off).
                    var error = steering.RingDistance >= 0f ? dist - steering.RingDistance : 0f;
                    var radial = dir * Mathf.Clamp(error, -1f, 1f);
                    var wish = radial + tangent * steering.Orbit * (Mathf.Abs(error) < 1.5f ? 1f : 0.3f);
                    if (wish.sqrMagnitude > 0.0004f)
                        transform.position += wish.normalized * (speed * Mathf.Clamp01(wish.magnitude + 0.35f) * dt);
                    KeepInArena();
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

        private void KeepInArena()
        {
            if (_arena.size.sqrMagnitude < 0.01f) return;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, _arena.min.x + _bodyRadius + 0.3f, _arena.max.x - _bodyRadius - 0.3f);
            p.z = Mathf.Clamp(p.z, _arena.min.z + _bodyRadius + 0.3f, _arena.max.z - _bodyRadius - 0.3f);
            transform.position = p;
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
            else if (_mind.RearingUp)
                c = new Color(0.5f, 0.12f, 0.02f) * (0.6f + 0.4f * Mathf.Sin(Time.time * 40f));
            else if (_mind.Intent == EnemyIntent.Buff)
            {
                // Charging up: a throbbing violet that builds toward the release.
                var k = _mind.BuffCastProgress;
                c = new Color(0.35f, 0.05f, 0.5f) * (0.3f + 0.7f * k) * (0.75f + 0.25f * Mathf.Sin(Time.time * 30f));
            }
            else if (_mind.Buffed)
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

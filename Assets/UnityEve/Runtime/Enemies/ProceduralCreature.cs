using System;
using UnityEngine;

namespace UnityEve.Enemies
{
    public enum LimbStyle
    {
        /// <summary>Steps in a gait: the hip swings, the knee lifts.</summary>
        Leg,
        /// <summary>Undulates along its length; whips on an attack.</summary>
        Tentacle,
        /// <summary>Hangs and swings like a knuckle-walker's arm; slams on an attack.</summary>
        Arm,
    }

    [Serializable]
    public sealed class CreatureLimb
    {
        /// <summary>The chain from the root joint outward; each is a child of the one before.</summary>
        public Transform[] Segments = Array.Empty<Transform>();
        public LimbStyle Style;
        /// <summary>Gait or wave offset in radians, so limbs move out of step.</summary>
        public float Phase;
        /// <summary>Swing in degrees at full stride.</summary>
        public float Amplitude = 25f;
        /// <summary>Local axis the limb swings around (forward/back).</summary>
        public Vector3 SwingAxis = Vector3.up;
        /// <summary>Local axis the limb lifts or curls around.</summary>
        public Vector3 LiftAxis = Vector3.right;
    }

    /// <summary>
    /// Animation for the alien creatures, which are built from parts rather than skinned and clipped.
    /// Legs step in a gait that speeds up with movement, tentacles undulate, arms swing; the body bobs,
    /// breathes or pulses; an attack lunges and flares the limbs, and death collapses it. Runs on scaled
    /// time so the world-pause freezes it mid-stride. Presentation only.
    /// </summary>
    public sealed class ProceduralCreature : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private CreatureLimb[] _limbs = Array.Empty<CreatureLimb>();
        [Tooltip("Gait cycles per metre per second of movement.")]
        [SerializeField] private float _stride = 2.2f;
        [SerializeField] private float _bob = 0.04f;
        [SerializeField] private bool _pulse;

        private Quaternion[][] _rest;
        private Vector3 _bodyPos, _bodyScale;
        private float _speed, _phase, _time;
        private float _attack = -1f, _death = -1f;

        /// <summary>Animation rate (stun slows it, Slow halves it).</summary>
        public float Playback { get; set; } = 1f;

        public void Configure(Transform body, CreatureLimb[] limbs, float stride, float bob, bool pulse)
        {
            _body = body;
            _limbs = limbs;
            _stride = stride;
            _bob = bob;
            _pulse = pulse;
        }

        public void SetSpeed(float metresPerSecond) => _speed = Mathf.Max(0f, metresPerSecond);
        public void Attack() => _attack = 0f;
        public void Die() => _death = 0f;

        private void Awake()
        {
            _rest = new Quaternion[_limbs.Length][];
            for (var l = 0; l < _limbs.Length; l++)
            {
                var segs = _limbs[l].Segments;
                _rest[l] = new Quaternion[segs.Length];
                for (var i = 0; i < segs.Length; i++) _rest[l][i] = segs[i] != null ? segs[i].localRotation : Quaternion.identity;
            }
            if (_body != null)
            {
                _bodyPos = _body.localPosition;
                _bodyScale = _body.localScale;
            }
        }

        private static float Bell(float k) => Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);

        private void Update()
        {
            var dt = Time.deltaTime * Playback;
            if (dt <= 0f) return;
            _time += dt;
            _phase += dt * Mathf.PI * 2f * (0.35f + _speed * _stride * 0.5f);
            var gait = Mathf.Clamp01(_speed / 1.4f);
            var strike = 0f;
            if (_attack >= 0f)
            {
                _attack += dt;
                strike = Bell(_attack / 0.45f);
                if (_attack > 0.45f) _attack = -1f;
            }
            var dead = 0f;
            if (_death >= 0f)
            {
                _death += dt;
                dead = Mathf.Clamp01(_death / 0.8f);
            }

            for (var l = 0; l < _limbs.Length; l++)
            {
                var limb = _limbs[l];
                var segs = limb.Segments;
                var p = _phase + limb.Phase;
                for (var i = 0; i < segs.Length; i++)
                {
                    if (segs[i] == null) continue;
                    var swing = 0f;
                    var lift = 0f;
                    switch (limb.Style)
                    {
                        case LimbStyle.Leg:
                            if (i == 0)
                            {
                                swing = limb.Amplitude * gait * Mathf.Sin(p) + 2f * Mathf.Sin(_time * 7f + limb.Phase) * (1f - gait);
                                lift = -limb.Amplitude * 0.6f * gait * Mathf.Max(0f, Mathf.Cos(p)) - 30f * strike;
                            }
                            else lift = limb.Amplitude * 0.7f * gait * Mathf.Max(0f, Mathf.Cos(p)) + 20f * strike;
                            break;
                        case LimbStyle.Tentacle:
                        {
                            var along = (i + 1f) / segs.Length;
                            var amp = limb.Amplitude * (0.35f + 0.65f * gait) * along;
                            swing = amp * Mathf.Sin(_time * 2.6f - i * 0.75f + limb.Phase) + 40f * strike * along;
                            lift = amp * 0.6f * Mathf.Cos(_time * 2.1f - i * 0.9f + limb.Phase);
                            break;
                        }
                        case LimbStyle.Arm:
                            swing = i == 0
                                ? limb.Amplitude * gait * Mathf.Sin(p) + 4f * Mathf.Sin(_time * 1.7f + limb.Phase) - 70f * strike
                                : limb.Amplitude * 0.4f * gait * Mathf.Sin(p - 0.6f) - 25f * strike;
                            break;
                    }
                    // Dying: everything curls in.
                    lift += 55f * dead;
                    segs[i].localRotation = _rest[l][i]
                        * Quaternion.AngleAxis(swing, limb.SwingAxis)
                        * Quaternion.AngleAxis(lift, limb.LiftAxis);
                }
            }

            if (_body != null)
            {
                var bob = _bob * (gait * Mathf.Abs(Mathf.Sin(_phase)) + 0.4f * Mathf.Sin(_time * 2f));
                var lunge = Vector3.forward * (0.18f * strike);
                _body.localPosition = _bodyPos + Vector3.up * bob + lunge - Vector3.up * (0.25f * dead);
                var breathe = _pulse ? 1f + 0.07f * Mathf.Sin(_time * 4.2f) : 1f + 0.02f * Mathf.Sin(_time * 2f);
                _body.localScale = new Vector3(_bodyScale.x * breathe * (1f + 0.25f * dead), _bodyScale.y * breathe * (1f - 0.5f * dead) * (1f + 0.12f * strike),
                    _bodyScale.z * breathe * (1f + 0.25f * dead));
            }
        }
    }
}

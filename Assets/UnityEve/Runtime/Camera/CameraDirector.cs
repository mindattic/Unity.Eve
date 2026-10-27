using System.Collections.Generic;
using UnityEngine;
using Unity.Eve.Core.Camera;

namespace Unity.Eve.Cameras
{
    /// <summary>
    /// Drives the one gameplay camera. Picks the active <see cref="CameraZone"/> via the pure
    /// <see cref="CameraZoneSelector"/>, hard-cuts between shots like a PS1 survival-horror game, lets
    /// CCTV/rail shots track the player, and adds trauma-based screen shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraDirector : MonoBehaviour
    {
        [SerializeField] private Transform _subject;
        [SerializeField] private float _subjectHeight = 1.1f;
        [SerializeField] private CameraShot _initialShot;

        private readonly CameraZoneSelector _selector = new CameraZoneSelector();
        private readonly Dictionary<int, CameraZone> _zones = new Dictionary<int, CameraZone>();
        /// <summary>Each zone's selector id: handed out in the order zones are first entered, from 0, so it is never
        /// the selector's 'no zone' (-1) and never depends on engine instance ids.</summary>
        private readonly Dictionary<CameraZone, int> _zoneIds = new Dictionary<CameraZone, int>();
        private Camera _camera;
        private CameraShot _active;
        private Pose _pose;
        private bool _snap = true;
        private float _trauma;
        private float _shakeTime;

        public static CameraDirector Instance { get; private set; }
        public Camera Camera => _camera;
        public CameraShot ActiveShot => _active;
        public int CutCount => _selector.CutCount;

        /// <summary>Camera yaw in radians (clockwise from +Z): the input basis for the movement stick.</summary>
        public float Yaw
        {
            get
            {
                var f = transform.forward;
                return Mathf.Atan2(f.x, f.z);
            }
        }

        public void Configure(Transform subject, CameraShot initialShot)
        {
            _subject = subject;
            _initialShot = initialShot;
        }

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            _active = _initialShot;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnterZone(CameraZone zone)
        {
            var id = IdOf(zone);
            _zones[id] = zone;
            _selector.Enter(id, zone.Priority);
            ApplySelection();
        }

        private int IdOf(CameraZone zone)
        {
            if (!_zoneIds.TryGetValue(zone, out var id)) _zoneIds[zone] = id = _zoneIds.Count;
            return id;
        }

        public void ExitZone(CameraZone zone)
        {
            _selector.Exit(IdOf(zone));
            ApplySelection();
        }

        /// <summary>After a teleport (doors), forget stale zones; triggers re-enter on the next physics step.</summary>
        public void ResetZones(CameraShot holdShot = null)
        {
            _selector.Clear();
            // Forget the zone itself too: otherwise re-entering the same CameraZone instance at the
            // destination (e.g. a door that loops back into the room it left) short-circuits in
            // ApplySelection's `zone == ActiveZone` check and ZoneChanged never re-fires.
            ActiveZone = null;
            if (holdShot != null) Cut(holdShot);
        }

        /// <summary>Raised when the player's screen (the winning camera zone) changes.</summary>
        public event System.Action<CameraZone> ZoneChanged;
        public CameraZone ActiveZone { get; private set; }

        private void ApplySelection()
        {
            if (_selector.ActiveZone < 0 || !_zones.TryGetValue(_selector.ActiveZone, out var zone)) return;
            if (zone.Shot != _active) Cut(zone.Shot);
            if (zone == ActiveZone) return;
            ActiveZone = zone;
            ZoneChanged?.Invoke(zone);
        }

        public void Cut(CameraShot shot)
        {
            _active = shot;
            _snap = true;
        }

        public void AddTrauma(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

        private void LateUpdate()
        {
            if (_active == null || _subject == null) return;

            // Camera motion runs on real time: CCTV servos keep tracking during slow-motion Focus.
            var dt = Time.unscaledDeltaTime;
            var subject = _subject.position + Vector3.up * _subjectHeight;
            _active.Evaluate(subject, _pose, _snap, dt, out _pose);
            _snap = false;
            _camera.fieldOfView = _active.FieldOfView;

            var position = _pose.position;
            if (_trauma > 0f && Time.timeScale > 0f)
            {
                _shakeTime += dt * 40f;
                var amp = _trauma * _trauma * 0.14f;
                position += new Vector3(
                    (Mathf.Sin(_shakeTime * 1.1f) + Mathf.Sin(_shakeTime * 2.3f)) * amp,
                    (Mathf.Sin(_shakeTime * 1.7f) + Mathf.Sin(_shakeTime * 3.1f)) * amp * 0.7f,
                    (Mathf.Sin(_shakeTime * 1.3f) + Mathf.Sin(_shakeTime * 2.9f)) * amp);
                _trauma = Mathf.Max(0f, _trauma - 1.6f * dt);
            }
            transform.SetPositionAndRotation(position, _pose.rotation);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEve.Core.Camera;

namespace UnityEve.Cameras
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
            var id = zone.GetInstanceID();
            _zones[id] = zone;
            _selector.Enter(id, zone.Priority);
            ApplySelection();
        }

        public void ExitZone(CameraZone zone)
        {
            _selector.Exit(zone.GetInstanceID());
            ApplySelection();
        }

        /// <summary>After a teleport (doors), forget stale zones; triggers re-enter on the next physics step.</summary>
        public void ResetZones(CameraShot holdShot = null)
        {
            _selector.Clear();
            if (holdShot != null) Cut(holdShot);
        }

        private void ApplySelection()
        {
            if (_selector.ActiveZone >= 0 && _zones.TryGetValue(_selector.ActiveZone, out var zone) && zone.Shot != _active)
                Cut(zone.Shot);
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

using UnityEngine;

namespace UnityEve.Cameras
{
    public enum ShotMode
    {
        /// <summary>Locked-off survival-horror framing: the camera never moves.</summary>
        Fixed,
        /// <summary>Mounted like a security camera: fixed position, servo-pans to follow the player within limits.</summary>
        Cctv,
        /// <summary>Slides along a rail to keep level with the player, looking along the authored direction.</summary>
        Rail,
    }

    /// <summary>
    /// One authored camera position. The transform is the camera's rest pose (position + rotation).
    /// The <see cref="CameraDirector"/> moves the single real camera onto whichever shot is active.
    /// </summary>
    public sealed class CameraShot : MonoBehaviour
    {
        [SerializeField] private ShotMode _mode = ShotMode.Fixed;
        [SerializeField, Range(20f, 90f)] private float _fieldOfView = 50f;

        [Header("CCTV")]
        [Tooltip("Max pan (yaw) away from the rest rotation, degrees.")]
        [SerializeField] private float _maxYaw = 50f;
        [SerializeField] private float _maxPitch = 25f;
        [Tooltip("Servo lag: higher tracks faster.")]
        [SerializeField] private float _servoSpeed = 2.2f;

        [Header("Rail")]
        [SerializeField] private Vector3 _railStart;
        [SerializeField] private Vector3 _railEnd;
        [SerializeField] private float _railFollow = 3f;

        public ShotMode Mode => _mode;
        public float FieldOfView => _fieldOfView;

        public void Configure(ShotMode mode, float fov, float maxYaw = 50f, float maxPitch = 25f)
        {
            _mode = mode;
            _fieldOfView = fov;
            _maxYaw = maxYaw;
            _maxPitch = maxPitch;
        }

        public void ConfigureRail(Vector3 start, Vector3 end)
        {
            _mode = ShotMode.Rail;
            _railStart = start;
            _railEnd = end;
        }

        /// <summary>The pose this shot wants this frame, given the tracked subject.</summary>
        public void Evaluate(Vector3 subject, Pose current, bool snap, float deltaTime, out Pose pose)
        {
            var rest = new Pose(transform.position, transform.rotation);
            switch (_mode)
            {
                case ShotMode.Cctv:
                {
                    var desired = ClampedLook(rest, subject);
                    var t = snap ? 1f : 1f - Mathf.Exp(-_servoSpeed * deltaTime);
                    pose = new Pose(rest.position, Quaternion.Slerp(current.rotation, desired, t));
                    return;
                }
                case ShotMode.Rail:
                {
                    var along = ProjectOnRail(subject);
                    var t = snap ? 1f : 1f - Mathf.Exp(-_railFollow * deltaTime);
                    var position = Vector3.Lerp(current.position, along, t);
                    pose = new Pose(position, ClampedLook(new Pose(position, rest.rotation), subject));
                    return;
                }
                default:
                    pose = rest;
                    return;
            }
        }

        private Quaternion ClampedLook(Pose rest, Vector3 subject)
        {
            var toSubject = subject - rest.position;
            if (toSubject.sqrMagnitude < 0.0001f) return rest.rotation;
            // Express the look direction in the shot's local frame, clamp yaw/pitch, convert back.
            var local = Quaternion.Inverse(rest.rotation) * toSubject.normalized;
            var yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -_maxYaw, _maxYaw);
            var pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -_maxPitch, _maxPitch);
            return rest.rotation * Quaternion.Euler(pitch, yaw, 0f);
        }

        private Vector3 ProjectOnRail(Vector3 subject)
        {
            var a = transform.parent != null ? transform.parent.TransformPoint(_railStart) : _railStart;
            var b = transform.parent != null ? transform.parent.TransformPoint(_railEnd) : _railEnd;
            var ab = b - a;
            var len2 = ab.sqrMagnitude;
            if (len2 < 0.0001f) return a;
            var t = Mathf.Clamp01(Vector3.Dot(subject - a, ab) / len2);
            return a + ab * t;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _mode == ShotMode.Cctv ? Color.red : _mode == ShotMode.Rail ? Color.cyan : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, _fieldOfView, 4f, 0.2f, 16f / 9f);
            Gizmos.matrix = Matrix4x4.identity;
            if (_mode == ShotMode.Rail)
            {
                var p = transform.parent;
                Gizmos.DrawLine(p ? p.TransformPoint(_railStart) : _railStart, p ? p.TransformPoint(_railEnd) : _railEnd);
            }
        }
    }
}

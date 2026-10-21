using UnityEngine;

namespace Unity.Eve.Cameras
{
    /// <summary>
    /// A trigger volume that selects a <see cref="CameraShot"/> while the player stands in it.
    /// Overlap neighbouring zones slightly: the overlap is the hysteresis that stops boundary flicker.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZone : MonoBehaviour
    {
        [SerializeField] private CameraShot _shot;
        [Tooltip("Higher wins when zones overlap (e.g. a doorway close-up inside a room zone).")]
        [SerializeField] private int _priority;

        public CameraShot Shot => _shot;
        public int Priority => _priority;

        public void Configure(CameraShot shot, int priority)
        {
            _shot = shot;
            _priority = priority;
        }

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && CameraDirector.Instance != null)
                CameraDirector.Instance.EnterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") && CameraDirector.Instance != null)
                CameraDirector.Instance.ExitZone(this);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.12f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
            if (_shot != null) Gizmos.DrawLine(transform.position, _shot.transform.position);
        }
    }
}

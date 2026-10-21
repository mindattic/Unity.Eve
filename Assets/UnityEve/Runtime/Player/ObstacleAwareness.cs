using System.Collections.Generic;
using UnityEngine;

namespace Unity.Eve.Player
{
    /// <summary>
    /// An outer "slow your roll" trigger around Kat, bigger than the CharacterController's own capsule
    /// (the hard, final collider). Walls and furniture inside it make <see cref="PlayerController"/> ease
    /// off her run speed before she actually makes contact, so she settles against them instead of
    /// jittering at full speed — a warning collider and a final one, not one collider doing both jobs.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class ObstacleAwareness : MonoBehaviour
    {
        private readonly HashSet<Collider> _nearby = new HashSet<Collider>();
        private CapsuleCollider _warning;

        /// <summary>The warning collider's radius, in world units (uniform-scale rigs only).</summary>
        public float Radius => _warning.radius * transform.lossyScale.x;

        private void Awake() => _warning = GetComponent<CapsuleCollider>();

        private void OnTriggerEnter(Collider other)
        {
            if (!other.isTrigger && other.GetComponentInParent<Enemies.EnemyActor>() == null) _nearby.Add(other);
        }

        private void OnTriggerExit(Collider other) => _nearby.Remove(other);

        /// <summary>The closest point of solid geometry inside the warning radius that's actually roughly
        /// ahead of <paramref name="direction"/> — not just the closest point overall. With several props
        /// in the (small) warning bubble at once, as is common now with benches/trees/cars dotted around
        /// town, the globally-closest one is often off to the side; picking that instead of whatever's
        /// actually in her path would miss the one she's about to walk into.</summary>
        public bool TryGetNearest(Vector3 from, Vector3 direction, out Vector3 point, out float distance)
        {
            point = default;
            distance = float.MaxValue;
            _nearby.RemoveWhere(c => c == null);
            var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            foreach (var c in _nearby)
            {
                var p = c.ClosestPoint(from);
                var toP = p - from;
                toP.y = 0f;
                if (dir != Vector3.zero && toP.sqrMagnitude > 0.0001f && Vector3.Dot(dir, toP.normalized) < 0.3f) continue;
                var d = Vector3.Distance(from, p);
                if (d < distance)
                {
                    distance = d;
                    point = p;
                }
            }
            return distance < float.MaxValue;
        }
    }
}

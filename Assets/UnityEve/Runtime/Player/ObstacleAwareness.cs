using System.Collections.Generic;
using UnityEngine;

namespace UnityEve.Player
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

        /// <summary>The closest point of solid geometry inside the warning radius, if anything is there.</summary>
        public bool TryGetNearest(Vector3 from, out Vector3 point, out float distance)
        {
            point = default;
            distance = float.MaxValue;
            _nearby.RemoveWhere(c => c == null);
            foreach (var c in _nearby)
            {
                var p = c.ClosestPoint(from);
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

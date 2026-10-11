using System.Collections.Generic;
using UnityEngine;

namespace UnityEve.Battle
{
    /// <summary>
    /// Marks a bench, streetlamp post or mailbox as cover geometry. The collider on this object (or a
    /// child) is what <see cref="Cover"/> raycasts against — there is no grid or angle table: how much
    /// protection a prop gives falls straight out of how much of it actually stands in the way.
    /// </summary>
    public sealed class CoverProp : MonoBehaviour
    {
        private static readonly List<CoverProp> All = new List<CoverProp>();
        internal static IReadOnlyList<CoverProp> Registry => All;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);
    }

    /// <summary>
    /// Cover read from real geometry: a fan of rays from the attacker toward Kat's silhouette shows how
    /// much of her a <see cref="CoverProp"/> actually blocks. A thin lamp post gives cover all the way
    /// around it, but only ever hides part of her; crouching behind a wide mailbox can block nearly all
    /// of the fan.
    /// </summary>
    public static class Cover
    {
        /// <summary>Lateral (x) and vertical (y) offsets sampling Kat's silhouette, metres from her feet.</summary>
        private static readonly Vector2[] Silhouette =
        {
            new Vector2(-0.18f, 0.25f), new Vector2(0f, 0.25f), new Vector2(0.18f, 0.25f),
            new Vector2(-0.18f, 0.85f), new Vector2(0f, 0.85f), new Vector2(0.18f, 0.85f),
            new Vector2(-0.18f, 1.45f), new Vector2(0f, 1.45f), new Vector2(0.18f, 1.45f),
        };

        /// <summary>0 (fully exposed) to 1 (fully hidden) of Kat's silhouette, as seen from <paramref name="attackerEye"/>.</summary>
        public static float Exposure(Vector3 defenderFeet, Vector3 attackerEye)
        {
            if (CoverProp.Registry.Count == 0) return 0f;
            var toDefender = defenderFeet - attackerEye;
            toDefender.y = 0f;
            if (toDefender.sqrMagnitude < 0.0001f) return 0f;
            var right = Vector3.Cross(Vector3.up, toDefender.normalized);
            var blocked = 0;
            foreach (var o in Silhouette)
            {
                var point = defenderFeet + right * o.x + Vector3.up * o.y;
                var toPoint = point - attackerEye;
                var dist = toPoint.magnitude;
                if (dist < 0.05f) continue;
                if (Physics.Raycast(attackerEye, toPoint / dist, out var hit, dist - 0.05f, ~0, QueryTriggerInteraction.Collide)
                    && hit.collider.GetComponentInParent<CoverProp>() != null)
                    blocked++;
            }
            return blocked / (float)Silhouette.Length;
        }
    }
}

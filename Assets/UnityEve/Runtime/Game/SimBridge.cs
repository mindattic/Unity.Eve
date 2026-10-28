using UnityEngine;
using Unity.Eve.Battle;
using Unity.Eve.Core.Sim;

namespace Unity.Eve.Game
{
    /// <summary>Converts between the authority's plain vectors and Unity's.</summary>
    public static class SimConvert
    {
        public static Vector3 ToUnity(this Vec3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Vec3 ToSim(this Vector3 v) => new Vec3(v.x, v.y, v.z);

        /// <summary>An upright rotation facing <paramref name="yaw"/> (radians clockwise from +Z).</summary>
        public static Quaternion YawRotation(float yaw) => Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);

        /// <summary>The yaw of a rotation's forward on the ground plane.</summary>
        public static float YawOf(Quaternion rotation)
        {
            var f = rotation * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-8f ? Mathf.Atan2(f.x, f.z) : 0f;
        }
    }

    /// <summary>Answers the authority's geometry questions with the scene's colliders.</summary>
    public sealed class UnityWorld : IWorldQueries
    {
        public static readonly UnityWorld Instance = new UnityWorld();

        public float CoverExposure(Vec3 feet, Vec3 attackerEye) => Cover.Exposure(feet.ToUnity(), attackerEye.ToUnity());
    }
}

using System;

namespace Unity.Eve.Core.Sim
{
    /// <summary>
    /// A plain 3D vector for the authority (Core has no engine types). The operations mirror the ones the
    /// MonoBehaviours used on <c>UnityEngine.Vector3</c> (same normalize threshold, same MoveTowards), so moving a
    /// rule into Core does not change its numbers. Y is up; yaw is radians clockwise from +Z.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Unit length, or zero for a vector shorter than 1e-5 (Unity's rule).</summary>
        public Vec3 Normalized
        {
            get
            {
                var m = Magnitude;
                return m > 1e-5f ? this / m : Zero;
            }
        }

        /// <summary>This vector on the ground plane (y = 0).</summary>
        public Vec3 Flat => new Vec3(X, 0f, Z);

        public Vec3 WithY(float y) => new Vec3(X, y, Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float k) => new Vec3(a.X * k, a.Y * k, a.Z * k);
        public static Vec3 operator *(float k, Vec3 a) => new Vec3(a.X * k, a.Y * k, a.Z * k);
        public static Vec3 operator /(Vec3 a, float k) => new Vec3(a.X / k, a.Y / k, a.Z / k);

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static float Distance(Vec3 a, Vec3 b) => (a - b).Magnitude;

        /// <summary>Linear interpolation with t clamped to 0..1.</summary>
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            t = SimMath.Clamp01(t);
            return new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
        public static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDelta)
        {
            var d = target - current;
            var sqr = d.SqrMagnitude;
            if (sqr == 0f || (maxDelta >= 0f && sqr <= maxDelta * maxDelta)) return target;
            var dist = (float)Math.Sqrt(sqr);
            return current + d / dist * maxDelta;
        }

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:R}, {1:R}, {2:R})", X, Y, Z);
    }

    /// <summary>The small float helpers the rules used from <c>Mathf</c>, plus yaw-only rotation.</summary>
    public static class SimMath
    {
        public const float Pi = (float)Math.PI;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Sin(float v) => (float)Math.Sin(v);

        /// <summary>The yaw a body faces when looking along (x, z): Unity's LookRotation, about the up axis only.</summary>
        public static float YawOf(Vec3 direction) => (float)Math.Atan2(direction.X, direction.Z);

        /// <summary>The horizontal unit forward of a yaw.</summary>
        public static Vec3 Forward(float yaw) => new Vec3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));

        /// <summary>
        /// Blends <paramref name="yaw"/> toward <paramref name="targetYaw"/> along the shortest arc by
        /// <paramref name="t"/> (clamped 0..1): the same turn a Quaternion.Slerp between two upright facings makes.
        /// </summary>
        public static float TurnToward(float yaw, float targetYaw, float t) =>
            Wrap(yaw + Camera.MovementBasis.DeltaAngle(yaw, targetYaw) * Clamp01(t));

        /// <summary>An angle in (-π, π].</summary>
        public static float Wrap(float a)
        {
            a %= 2f * Pi;
            if (a > Pi) a -= 2f * Pi;
            if (a <= -Pi) a += 2f * Pi;
            return a;
        }
    }
}

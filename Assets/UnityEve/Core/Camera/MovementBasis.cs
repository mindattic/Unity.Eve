using System;

namespace Unity.Eve.Core.Camera
{
    /// <summary>
    /// Keeps "stick up" meaning the same world direction across fixed-camera cuts, so a held stick
    /// does not suddenly reverse the player on a cut. After a short grace period the basis rotates
    /// toward the new camera along the shortest arc; releasing the stick adopts it instantly.
    /// Angles are yaw in radians, measured clockwise from world +Z (Unity's forward).
    /// </summary>
    public sealed class MovementBasis
    {
        public const float GraceSeconds = 0.16f;
        public const float TurnRateRadPerSec = 0.9f * (float)Math.PI;

        private float _targetYaw;
        private float _graceRemaining;
        private bool _initialized;

        public float Yaw { get; private set; }

        public void Tick(float deltaSeconds, float cameraYaw, bool stickHeld)
        {
            if (!_initialized || !stickHeld)
            {
                Yaw = _targetYaw = cameraYaw;
                _graceRemaining = 0f;
                _initialized = true;
                return;
            }

            if (Math.Abs(DeltaAngle(_targetYaw, cameraYaw)) > 1e-4f)
            {
                _targetYaw = cameraYaw;
                _graceRemaining = GraceSeconds;
            }

            if (_graceRemaining > 0f)
            {
                _graceRemaining -= deltaSeconds;
                return;
            }

            var delta = DeltaAngle(Yaw, _targetYaw);
            var step = TurnRateRadPerSec * deltaSeconds;
            Yaw = Math.Abs(delta) <= step ? _targetYaw : Yaw + Math.Sign(delta) * step;
        }

        /// <summary>Converts stick input (x right, y up) to a world XZ direction under the current basis.</summary>
        public (float x, float z) ToWorld(float stickX, float stickY)
        {
            var fx = (float)Math.Sin(Yaw);
            var fz = (float)Math.Cos(Yaw);
            // Unity is left-handed: right = cross(up, forward) = (fz, -fx).
            return (fx * stickY + fz * stickX, fz * stickY - fx * stickX);
        }

        /// <summary>Shortest signed angle from a to b, in (-π, π].</summary>
        public static float DeltaAngle(float a, float b)
        {
            var d = (b - a) % (2f * (float)Math.PI);
            if (d > Math.PI) d -= 2f * (float)Math.PI;
            if (d <= -Math.PI) d += 2f * (float)Math.PI;
            return d;
        }
    }
}

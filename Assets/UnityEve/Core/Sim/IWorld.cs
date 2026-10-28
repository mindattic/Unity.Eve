namespace Unity.Eve.Core.Sim
{
    /// <summary>
    /// The one piece of a player's body the authority cannot compute itself: collide-and-slide against level
    /// geometry. Presentation implements it over the CharacterController; tests use a flat floor.
    /// </summary>
    public interface IBodyPhysics
    {
        Vec3 Position { get; }
        /// <summary>Moves by <paramref name="motion"/>, sliding along whatever is in the way.</summary>
        void Move(Vec3 motion);
        /// <summary>True when the last move ended on the ground.</summary>
        bool IsGrounded { get; }
        /// <summary>Places the body without collision.</summary>
        void Teleport(Vec3 position);
        /// <summary>Shows the body facing <paramref name="yaw"/> (the authority owns the facing).</summary>
        void SetYaw(float yaw);
        /// <summary>The nearest solid geometry roughly ahead of <paramref name="direction"/> inside the
        /// "slow your roll" radius, if any.</summary>
        bool TryObstacleAhead(Vec3 from, Vec3 direction, out float distance);
        /// <summary>The radius of that warning zone.</summary>
        float ObstacleRadius { get; }
    }

    /// <summary>Queries of level geometry the rules need (presentation answers them with raycasts).</summary>
    public interface IWorldQueries
    {
        /// <summary>0 (fully exposed) to 1 (fully hidden) of a player standing at <paramref name="feet"/>, as
        /// seen from <paramref name="attackerEye"/>.</summary>
        float CoverExposure(Vec3 feet, Vec3 attackerEye);
    }

    /// <summary>No geometry: nothing ever gives cover.</summary>
    public sealed class OpenWorld : IWorldQueries
    {
        public static readonly OpenWorld Instance = new OpenWorld();
        public float CoverExposure(Vec3 feet, Vec3 attackerEye) => 0f;
    }
}

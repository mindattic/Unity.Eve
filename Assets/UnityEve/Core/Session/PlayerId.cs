using System;

namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// A player, by seat. Everything that belongs to a player is keyed by this, never by a global
    /// "the player"; which seat this machine controls is answered only by <see cref="ISession.LocalPlayer"/>.
    /// Online, the seat is the Unity.Multiplayer seat index (NetSender.SeatIndex).
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(int seat) => Seat = seat;

        /// <summary>Zero-based seat index.</summary>
        public int Seat { get; }

        public static PlayerId Of(int seat) => new PlayerId(seat);

        public bool Equals(PlayerId other) => Seat == other.Seat;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Seat;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Seat == b.Seat;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Seat != b.Seat;

        /// <summary>"P1" for seat 0.</summary>
        public override string ToString() => $"P{Seat + 1}";
    }
}

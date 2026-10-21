using System;

namespace Unity.Eve.Core.Battle
{
    public enum FocusMove
    {
        /// <summary>Free small heal.</summary>
        QuickHeal,
        /// <summary>A free, guaranteed critical shot at the attacker's weak point.</summary>
        TargetVitals,
        /// <summary>A guaranteed full-damage counter-strike: knocks the attacker back and stuns it.</summary>
        Riposte,
    }

    /// <summary>
    /// Focus: the optional skill layer on top of ATB. A dodge timed so that the strike lands within
    /// <see cref="PerfectWindowSeconds"/> of the dodge starting is a perfect dodge; it slows the world
    /// and offers one free <see cref="FocusMove"/>. Missing the timing still evades (i-frames), so the
    /// game never requires it — it only rewards it.
    /// </summary>
    public sealed class FocusWindow
    {
        public const float PerfectWindowSeconds = 0.3f;
        public const float DurationRealSeconds = 2.4f;
        public const float SlowTimeScale = 0.15f;
        public const int QuickHealAmount = 25;
        public const float VitalsMultiplier = 3f;
        public const float RiposteStunSeconds = 2f;

        private float _remaining;

        public bool IsOpen => _remaining > 0f;
        /// <summary>0..1 of the window left, for the HUD ring.</summary>
        public float Remaining01 => _remaining / DurationRealSeconds;
        public int PerfectDodges { get; private set; }

        public event Action Opened;
        public event Action Closed;

        /// <summary>World time scale while the window is open; 1 otherwise.</summary>
        public float TimeScale => IsOpen ? SlowTimeScale : 1f;

        /// <summary>True when a strike landing <paramref name="secondsSinceDodgeStarted"/> after the dodge counts as perfect.</summary>
        public static bool IsPerfect(float secondsSinceDodgeStarted) =>
            secondsSinceDodgeStarted >= 0f && secondsSinceDodgeStarted <= PerfectWindowSeconds;

        public void Open()
        {
            var wasOpen = IsOpen;
            _remaining = DurationRealSeconds;
            PerfectDodges++;
            if (!wasOpen) Opened?.Invoke();
        }

        /// <summary>Spends the window. Returns false if the window had already closed.</summary>
        public bool TryUse()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }

        /// <param name="realDeltaSeconds">Unscaled time: the window must not slow itself down.</param>
        public void Tick(float realDeltaSeconds)
        {
            if (!IsOpen) return;
            _remaining -= realDeltaSeconds;
            if (_remaining <= 0f) Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _remaining = 0f;
            Closed?.Invoke();
        }
    }
}

using UnityEve.Core.Util;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// Gears-of-War-style active reload: a marker sweeps once across the bar while the magazine loads.
    /// Pressing Reload again while it sits in the sweet spot finishes the reload immediately instead of
    /// waiting out the rest of the bar. Missing it, or never pressing at all, still finishes as an
    /// ordinary reload at the normal speed — this is a bonus, never a requirement.
    /// </summary>
    public sealed class ActiveReload
    {
        public float Duration { get; }
        public float SweetSpotStart { get; }
        public float SweetSpotEnd { get; }
        private float _elapsed;

        public ActiveReload(float duration, Rng rng)
        {
            Duration = duration;
            var width = duration * BattleConfig.ActiveReloadSweetSpotWidth;
            // Keep the whole window clear of both ends so it always has a visible run-up and tail.
            var latestStart = duration * 0.78f - width;
            var earliestStart = duration * 0.3f;
            SweetSpotStart = rng.Range(earliestStart, latestStart);
            SweetSpotEnd = SweetSpotStart + width;
        }

        /// <summary>0..1 of the bar filled so far.</summary>
        public float Progress01 => _elapsed <= 0f ? 0f : _elapsed >= Duration ? 1f : _elapsed / Duration;
        public float SweetSpotStart01 => SweetSpotStart / Duration;
        public float SweetSpotEnd01 => SweetSpotEnd / Duration;
        public bool Finished => _elapsed >= Duration;
        public bool InSweetSpot => _elapsed >= SweetSpotStart && _elapsed <= SweetSpotEnd;

        public void Tick(float deltaSeconds) => _elapsed += deltaSeconds;
    }
}

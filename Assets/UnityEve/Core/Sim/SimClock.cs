using System;

namespace Unity.Eve.Core.Sim
{
    /// <summary>
    /// The authority's fixed tick (U.EVE-LAW-6). Real time is cut into ticks of exactly
    /// <see cref="TickSeconds"/>; inside each tick the world advances by that much times the tick's time
    /// scale (0 while paused, slower in Focus and bullet time). <see cref="Now"/> is the scaled clock every
    /// rule reads in place of <c>Time.time</c>, so a run depends only on its ticks, never on frame rate.
    /// </summary>
    public sealed class SimClock
    {
        public const float TickSeconds = 1f / 60f;
        /// <summary>A hitch never turns into a burst of more ticks than this in one frame.</summary>
        public const int MaxTicksPerAdvance = 8;

        private double _accumulator;
        private double _now;
        private double _realNow;

        /// <summary>Ticks stepped so far.</summary>
        public long Tick { get; private set; }
        /// <summary>Scaled seconds since the clock started (the rules' "now").</summary>
        public float Now => (float)_now;
        /// <summary>Unscaled seconds of ticks stepped so far.</summary>
        public float RealNow => (float)_realNow;
        /// <summary>Scaled seconds the current (or last) tick advanced.</summary>
        public float Delta { get; private set; }
        /// <summary>Time scale of the current (or last) tick.</summary>
        public float Scale { get; private set; } = 1f;
        /// <summary>0..1 of a tick of real time banked toward the next one, for interpolating presentation.</summary>
        public float Alpha => (float)(_accumulator / TickSeconds);

        /// <summary>Banks real time and returns how many whole ticks are due.</summary>
        public int Accumulate(float realDeltaSeconds)
        {
            if (realDeltaSeconds > 0f) _accumulator += realDeltaSeconds;
            var due = (int)Math.Floor(_accumulator / TickSeconds);
            if (due > MaxTicksPerAdvance)
            {
                due = MaxTicksPerAdvance;
                _accumulator = TickSeconds * 0.999;
                return due;
            }
            _accumulator -= due * (double)TickSeconds;
            if (_accumulator < 0) _accumulator = 0;
            return due;
        }

        /// <summary>Scaled seconds now, at full precision (the replay log's battle line keeps it).</summary>
        public double NowExact => _now;
        public double RealNowExact => _realNow;

        /// <summary>A replay starting mid-session: put the clock where the recording's was.</summary>
        internal void Restore(long tick, double now, double realNow)
        {
            Tick = tick;
            _now = now;
            _realNow = realNow;
            _accumulator = 0;
        }

        /// <summary>Starts one tick at <paramref name="timeScale"/>; returns its scaled delta.</summary>
        public float Begin(float timeScale)
        {
            Scale = timeScale < 0f ? 0f : timeScale;
            Delta = TickSeconds * Scale;
            Tick++;
            _now += Delta;
            _realNow += TickSeconds;
            return Delta;
        }
    }
}

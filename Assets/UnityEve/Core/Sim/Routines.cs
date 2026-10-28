using System.Collections;
using System.Collections.Generic;

namespace Unity.Eve.Core.Sim
{
    /// <summary>Yielded from a routine: resume once this many scaled seconds have passed on the authority clock.</summary>
    public sealed class Wait
    {
        public Wait(float seconds) => Seconds = seconds;
        public float Seconds { get; }
    }

    /// <summary>
    /// Plain C# iterators stepped by the authority's tick, in place of engine coroutines: a turn that plays out
    /// over time (a burst of shots, a dash, a thrown grenade) is a routine. A routine runs to its first yield the
    /// moment it starts. Yield null to wait one tick, a <see cref="Wait"/> to wait scaled seconds (resuming on
    /// the first tick at or past the deadline), or another iterator to run it to the end first.
    /// </summary>
    public sealed class RoutineRunner
    {
        private sealed class Running
        {
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
            public float ResumeAt = float.NegativeInfinity;
        }

        private readonly SimClock _clock;
        private readonly List<Running> _running = new List<Running>();

        public RoutineRunner(SimClock clock) => _clock = clock;

        public int Count => _running.Count;

        public void Start(IEnumerator routine)
        {
            if (routine == null) return;
            var r = new Running();
            r.Stack.Push(routine);
            if (Step(r)) _running.Add(r);
        }

        /// <summary>Resumes every routine that is due; called once per tick after the rules have run.</summary>
        public void Tick()
        {
            // Routines started during this pass wait for the next tick, like a coroutine started in Update.
            var count = _running.Count;
            for (var i = 0; i < count && i < _running.Count; i++)
            {
                var r = _running[i];
                if (_clock.Now < r.ResumeAt) continue;
                if (!Step(r))
                {
                    _running.RemoveAt(i);
                    i--;
                    count--;
                }
            }
        }

        public void StopAll() => _running.Clear();

        /// <returns>False when the routine finished.</returns>
        private bool Step(Running r)
        {
            r.ResumeAt = float.NegativeInfinity;
            while (r.Stack.Count > 0)
            {
                var top = r.Stack.Peek();
                if (!top.MoveNext())
                {
                    r.Stack.Pop();
                    continue;
                }
                switch (top.Current)
                {
                    case null:
                        return true;
                    case Wait w:
                        r.ResumeAt = _clock.Now + w.Seconds;
                        return true;
                    case IEnumerator nested:
                        r.Stack.Push(nested);
                        continue;
                    default:
                        return true;
                }
            }
            return false;
        }
    }
}

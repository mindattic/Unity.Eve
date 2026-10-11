using System;
using System.Collections.Generic;

namespace UnityEve.Core.Story
{
    /// <summary>One step of the night: shown until its fact becomes true.</summary>
    public sealed class Objective
    {
        public readonly string Text;
        /// <summary>The fact that completes it, e.g. "item:key_shed", "weapon:shotgun", "flag:church_tried".</summary>
        public readonly string Fact;

        public Objective(string text, string fact)
        {
            Text = text;
            Fact = fact;
        }
    }

    /// <summary>
    /// The story spine of the vertical slice. Facts are plain strings the game learns as it is played
    /// (an item picked up, a weapon taken, a door tried, an encounter cleared); facts are never
    /// forgotten. The current objective is the first one whose fact is not yet known, so objectives
    /// can be completed out of order and the tracker simply skips ahead.
    /// </summary>
    public sealed class Objectives
    {
        private readonly List<Objective> _steps;
        private readonly HashSet<string> _facts = new HashSet<string>();

        public Objectives(IEnumerable<Objective> steps) => _steps = new List<Objective>(steps);

        public IReadOnlyCollection<string> Facts => _facts;
        public int Count => _steps.Count;
        public event Action<Objective> Changed;
        public event Action Completed;

        /// <summary>The objective to show, or null when the story is done.</summary>
        public Objective Current
        {
            get
            {
                foreach (var s in _steps)
                    if (!_facts.Contains(s.Fact)) return s;
                return null;
            }
        }

        public int Index
        {
            get
            {
                for (var i = 0; i < _steps.Count; i++)
                    if (!_facts.Contains(_steps[i].Fact)) return i;
                return _steps.Count;
            }
        }

        public bool Done => Current == null;
        public bool Knows(string fact) => _facts.Contains(fact);

        /// <returns>True when the fact was new.</returns>
        public bool Learn(string fact)
        {
            if (string.IsNullOrEmpty(fact)) return false;
            var before = Current;
            if (!_facts.Add(fact)) return false;
            var after = Current;
            if (after != before)
            {
                if (after == null) Completed?.Invoke();
                else Changed?.Invoke(after);
            }
            return true;
        }

        /// <summary>Restores facts from a save without raising events.</summary>
        public void Restore(IEnumerable<string> facts)
        {
            _facts.Clear();
            foreach (var f in facts) _facts.Add(f);
        }
    }
}

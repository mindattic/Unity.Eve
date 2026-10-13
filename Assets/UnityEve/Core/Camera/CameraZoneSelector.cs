using System.Collections.Generic;

namespace UnityEve.Core.Camera
{
    /// <summary>
    /// Picks the active fixed-camera zone from the zones the player currently stands in.
    /// Rules: the highest priority wins; ties go to the zone entered most recently; when the player
    /// stands in no zone (a doorway gap), the current shot is held. Overlapping trigger volumes give
    /// natural hysteresis, so the camera does not flicker on a boundary.
    /// </summary>
    public sealed class CameraZoneSelector
    {
        private readonly List<(int id, int priority, long order)> _inside = new List<(int, int, long)>();
        private long _enterCounter;

        public int ActiveZone { get; private set; } = -1;
        public int CutCount { get; private set; }

        public void Enter(int zoneId, int priority)
        {
            // Dedupe without Exit's own Reselect: a zone with several overlapping colliders can
            // redundantly re-enter while already active, and an intermediate reselect here would
            // transiently drop it, cut to whatever else is inside, then cut straight back —
            // double-counting one "entry" as two hard cuts.
            _inside.RemoveAll(z => z.id == zoneId);
            _inside.Add((zoneId, priority, ++_enterCounter));
            Reselect();
        }

        public void Exit(int zoneId)
        {
            var removed = _inside.RemoveAll(z => z.id == zoneId) > 0;
            if (removed) Reselect();
        }

        public void Clear()
        {
            _inside.Clear();
            ActiveZone = -1;
        }

        private void Reselect()
        {
            if (_inside.Count == 0) return; // hold the current shot
            var best = _inside[0];
            foreach (var z in _inside)
                if (z.priority > best.priority || (z.priority == best.priority && z.order > best.order))
                    best = z;
            if (best.id == ActiveZone) return;
            ActiveZone = best.id;
            CutCount++;
        }
    }
}

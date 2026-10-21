using System.Collections.Generic;
using UnityEngine;

namespace Unity.Eve.Game
{
    /// <summary>
    /// Single owner of <see cref="Time.timeScale"/>. Systems request a pause (menus, targeting) or a
    /// slow-motion scale (Focus) by key; the world runs at the slowest requested speed. Nobody else
    /// writes timeScale, so overlapping requests never fight.
    /// </summary>
    public sealed class WorldTime : MonoBehaviour
    {
        private readonly HashSet<object> _pauses = new HashSet<object>();
        private readonly Dictionary<object, float> _scales = new Dictionary<object, float>();

        public static WorldTime Instance { get; private set; }
        public bool IsPaused => _pauses.Count > 0;

        private void Awake()
        {
            Instance = this;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = 1f;
        }

        public void Pause(object key)
        {
            _pauses.Add(key);
            Apply();
        }

        public void Resume(object key)
        {
            _pauses.Remove(key);
            Apply();
        }

        public void SetScale(object key, float scale)
        {
            if (scale >= 1f) _scales.Remove(key);
            else _scales[key] = Mathf.Max(0f, scale);
            Apply();
        }

        public void ClearAll()
        {
            _pauses.Clear();
            _scales.Clear();
            Apply();
        }

        private void Apply()
        {
            var scale = 1f;
            foreach (var s in _scales.Values) scale = Mathf.Min(scale, s);
            Time.timeScale = _pauses.Count > 0 ? 0f : scale;
        }
    }
}

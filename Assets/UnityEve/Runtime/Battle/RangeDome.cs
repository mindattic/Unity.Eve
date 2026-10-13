using UnityEngine;

namespace UnityEve.Battle
{
    /// <summary>
    /// Parasite Eve's range dome: a translucent hemisphere of hexagon cells centred on the player whose
    /// radius is the weapon's range. Only enemies inside it can be targeted. Also owns the target marker.
    /// </summary>
    public sealed class RangeDome : MonoBehaviour
    {
        private static readonly int UnscaledTime = Shader.PropertyToID("_UnscaledTime");

        [SerializeField] private Transform _dome;
        [SerializeField] private Transform _marker;

        private float _grow;
        private float _radius;

        public void Configure(Transform dome, Transform marker)
        {
            _dome = dome;
            _marker = marker;
        }

        private void Awake() => Hide();

        public void Show(Vector3 centre, float radius)
        {
            transform.position = centre;
            _radius = radius;
            if (_dome == null) return;
            if (!_dome.gameObject.activeSelf) _grow = 0f;
            _dome.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_dome != null) _dome.gameObject.SetActive(false);
            if (_marker != null) _marker.gameObject.SetActive(false);
        }

        public void Mark(Vector3? point)
        {
            if (_marker == null) return;
            _marker.gameObject.SetActive(point.HasValue);
            if (point.HasValue) _marker.position = point.Value + Vector3.up * 0.6f;
        }

        private void Update()
        {
            // Unscaled: the dome blooms in, and its hexagons scan, while the world is paused.
            if (_dome != null && _dome.gameObject.activeSelf)
            {
                Shader.SetGlobalFloat(UnscaledTime, Time.unscaledTime);
                _grow = Mathf.Min(1f, _grow + Time.unscaledDeltaTime * 2.5f);
                var s = 1f - Mathf.Pow(1f - _grow, 3f);
                _dome.localScale = Vector3.one * (_radius * 2f * s);
            }
            if (_marker != null && _marker.gameObject.activeSelf)
            {
                _marker.Rotate(0f, 180f * Time.unscaledDeltaTime, 0f, Space.World);
                _marker.localScale = Vector3.one * (0.22f + Mathf.Sin(Time.unscaledTime * 8f) * 0.03f);
            }
        }
    }
}

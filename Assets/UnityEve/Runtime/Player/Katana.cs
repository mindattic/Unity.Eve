using UnityEngine;

namespace UnityEve.Player
{
    /// <summary>
    /// Kat's katana, carried alongside her gun. Worn across her back; drawn into her right hand for the
    /// blade arts, with a light trail off the tip while it swings. Presentation only: the battle director
    /// decides when it is drawn and what it hits.
    /// </summary>
    public sealed class Katana : MonoBehaviour
    {
        [SerializeField] private Transform _blade;
        [SerializeField] private Transform _handAnchor;
        [SerializeField] private Transform _backAnchor;
        [SerializeField] private TrailRenderer _trail;

        public bool Drawn { get; private set; }

        public void Configure(Transform blade, Transform handAnchor, Transform backAnchor, TrailRenderer trail)
        {
            _blade = blade;
            _handAnchor = handAnchor;
            _backAnchor = backAnchor;
            _trail = trail;
        }

        private void Awake() => Sheathe();

        public void Draw()
        {
            Attach(_handAnchor);
            Drawn = true;
        }

        public void Sheathe()
        {
            Trail(false);
            Attach(_backAnchor);
            Drawn = false;
        }

        public void Trail(bool on)
        {
            if (_trail == null) return;
            if (on) _trail.Clear();
            _trail.emitting = on;
        }

        private void Attach(Transform anchor)
        {
            if (_blade == null || anchor == null) return;
            _blade.SetParent(anchor, false);
            _blade.localPosition = Vector3.zero;
            _blade.localRotation = Quaternion.identity;
        }
    }
}

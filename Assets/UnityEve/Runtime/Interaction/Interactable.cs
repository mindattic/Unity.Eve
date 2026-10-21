using UnityEngine;
using Unity.Eve.Player;

namespace Unity.Eve.Interaction
{
    /// <summary>Anything the player can use with the Interact button: doors, drawers, items, notes.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private float _radius = 1.4f;
        [SerializeField] private Vector3 _focusOffset = new Vector3(0f, 0.8f, 0f);

        [Tooltip("Local offset of the spot a player stands on to use this (used by the autopilot).")]
        [SerializeField] private Vector3 _approach = new Vector3(0f, 0f, 1f);

        public float Radius => _radius;
        public Vector3 ApproachPoint => transform.TransformPoint(_approach);
        public Vector3 FocusPoint => transform.position + transform.rotation * _focusOffset;

        /// <summary>Text after the button glyph, e.g. "Search drawer". Null hides the prompt.</summary>
        public abstract string Prompt { get; }
        public virtual bool CanInteract => isActiveAndEnabled;

        /// <summary>Hold-to-use duration in seconds; 0 for a press.</summary>
        public virtual float HoldSeconds => 0f;

        public abstract void Interact(PlayerCharacter player);

        /// <summary>Called once per <see cref="HoldSeconds"/> elapsed while Interact stays held. The base
        /// behaviour finishes in one stage; a container overrides this to keep rummaging for more while
        /// the player keeps holding. Returns true to start another stage, false to end the hold.</summary>
        public virtual bool InteractStage(PlayerCharacter player)
        {
            Interact(player);
            return false;
        }

        protected virtual void OnEnable() => Interactor.Register(this);
        protected virtual void OnDisable() => Interactor.Unregister(this);

        public void SetRadius(float radius) => _radius = radius;
        public void SetFocusOffset(Vector3 offset) => _focusOffset = offset;

        /// <summary>Sets where a player stands to use this, as a world position.</summary>
        public void SetApproachWorld(Vector3 world) => _approach = transform.InverseTransformPoint(world);

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(FocusPoint, _radius);
        }
    }
}

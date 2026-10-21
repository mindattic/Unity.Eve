using System.Collections;
using UnityEngine;
using Unity.Eve.Data;
using Unity.Eve.Player;
using Unity.Eve.UI;

namespace Unity.Eve.Interaction
{
    /// <summary>
    /// A locked barrier the player opens in place rather than passes through to another scene point —
    /// a fence gate, a chained door, a padlocked turnstile. Shares <see cref="Door"/>'s key/consume
    /// pattern, but swings a hinge open and disables its own blocking collider instead of teleporting.
    /// </summary>
    public sealed class Gate : Interactable
    {
        [SerializeField] private string _label = "Open";
        [SerializeField] private ItemDefinition _requiredKey;
        [SerializeField] private bool _consumeKey;
        [SerializeField] private string _lockedText = "It's locked.";
        [SerializeField] private Transform _hinge;
        [SerializeField] private float _openYaw = 100f;
        [SerializeField] private Collider _barrier;
        [Tooltip("Story fact learned when the player tries this gate while it is locked / when it opens.")]
        [SerializeField] private string _factOnTry;
        [SerializeField] private string _factOnOpen;

        private bool _unlocked;
        private bool _busy;

        public bool Locked => _requiredKey != null && !_unlocked;
        public bool Unlocked => _unlocked;
        /// <summary>Loading a save: it was already opened with its key.</summary>
        public void Unlock()
        {
            _unlocked = true;
            if (_barrier != null) _barrier.enabled = false;
            if (_hinge != null) _hinge.localRotation = Quaternion.Euler(0f, _openYaw, 0f);
        }

        public void SetStoryFacts(string onTry, string onOpen)
        {
            _factOnTry = onTry;
            _factOnOpen = onOpen;
        }

        public override string Prompt => _label;
        public override bool CanInteract => base.CanInteract && !_busy && !_unlocked;

        public void Configure(string label, Transform hinge, Collider barrier, ItemDefinition requiredKey = null, string lockedText = null)
        {
            _label = label;
            _hinge = hinge;
            _barrier = barrier;
            _requiredKey = requiredKey;
            if (lockedText != null) _lockedText = lockedText;
        }

        public override void Interact(PlayerCharacter player)
        {
            if (_requiredKey != null && !_unlocked)
            {
                if (player.Bag.CountOf(_requiredKey.Id) == 0)
                {
                    Game.StoryDirector.Instance?.Learn(_factOnTry);
                    AudioService.Play(SoundId.DoorLocked);
                    GameHud.Instance?.ShowMessage(_lockedText);
                    return;
                }
                if (_consumeKey) player.Bag.TryRemove(_requiredKey.Id);
                _unlocked = true;
                GameHud.Instance?.ShowMessage($"Used the {_requiredKey.Stats.DisplayName}.");
            }
            Game.StoryDirector.Instance?.Learn(_factOnOpen);
            StartCoroutine(Swing());
        }

        private IEnumerator Swing()
        {
            _busy = true;
            AudioService.Play(SoundId.DoorOpen);
            if (_barrier != null) _barrier.enabled = false;
            var from = _hinge != null ? _hinge.localRotation : Quaternion.identity;
            var to = Quaternion.Euler(0f, _openYaw, 0f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
            {
                if (_hinge != null) _hinge.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            if (_hinge != null) _hinge.localRotation = to;
            _busy = false;
        }

        private void OnDisable()
        {
            // Mirrors Door's cleanup: if the gate is disabled/destroyed mid-swing, don't leave
            // _busy stuck true forever (CanInteract would never recover if it's re-enabled).
            _busy = false;
        }
    }
}

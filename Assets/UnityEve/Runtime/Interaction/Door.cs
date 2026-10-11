using System.Collections;
using UnityEngine;
using UnityEve.Cameras;
using UnityEve.Data;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Interaction
{
    /// <summary>
    /// A door into (or out of) a building. Classic survival-horror transition: fade to black, door
    /// sound, the player reappears at the linked spawn point, fade in. Optionally needs a key item.
    /// </summary>
    public sealed class Door : Interactable
    {
        [SerializeField] private string _label = "Enter";
        [SerializeField] private Transform _destination;
        [SerializeField] private ItemDefinition _requiredKey;
        [SerializeField] private bool _consumeKey;
        [SerializeField] private string _lockedText = "It's locked.";

        private bool _unlocked;
        private bool _busy;

        public Transform Destination => _destination;
        public bool Locked => _requiredKey != null && !_unlocked;
        public override string Prompt => _label;
        public override bool CanInteract => base.CanInteract && !_busy;

        public void Configure(string label, Transform destination, ItemDefinition requiredKey = null, string lockedText = null)
        {
            _label = label;
            _destination = destination;
            _requiredKey = requiredKey;
            if (lockedText != null) _lockedText = lockedText;
        }

        public override void Interact(PlayerCharacter player)
        {
            if (_requiredKey != null && !_unlocked)
            {
                if (player.Bag.CountOf(_requiredKey.Id) == 0)
                {
                    AudioService.Play(SoundId.DoorLocked);
                    GameHud.Instance?.ShowMessage(_lockedText);
                    return;
                }
                if (_consumeKey) player.Bag.TryRemove(_requiredKey.Id);
                _unlocked = true;
                GameHud.Instance?.ShowMessage($"Used the {_requiredKey.Stats.DisplayName}.");
            }
            StartCoroutine(Transition(player));
        }

        private IEnumerator Transition(PlayerCharacter player)
        {
            _busy = true;
            var hud = GameHud.Instance;
            player.Controller.Lock();
            AudioService.Play(SoundId.DoorOpen);
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                hud?.SetFade(t);
                yield return null;
            }
            hud?.SetFade(1f);
            player.Controller.Teleport(_destination.position, _destination.rotation);
            CameraDirector.Instance?.ResetZones();
            // Two physics steps so the camera zone triggers at the destination fire before we fade in.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSecondsRealtime(0.25f);
            for (var t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.35f)
            {
                hud?.SetFade(t);
                yield return null;
            }
            hud?.SetFade(0f);
            player.Controller.Unlock();
            _busy = false;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEve.Battle;
using UnityEve.Input;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Interaction
{
    /// <summary>
    /// Finds the best interactable in front of the player, shows its prompt, and handles press or
    /// hold-to-use (rummaging takes a moment and roots the player, so searching a room has a cost).
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class Interactor : MonoBehaviour
    {
        private static readonly List<Interactable> All = new List<Interactable>();

        private PlayerCharacter _player;
        private Interactable _current;
        private Interactable _holding;
        private float _held;
        private string _holdLabel;

        public Interactable Current => _current;

        public static void Register(Interactable i)
        {
            if (!All.Contains(i)) All.Add(i);
        }

        public static void Unregister(Interactable i) => All.Remove(i);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        private void Awake() => _player = GetComponent<PlayerCharacter>();

        private void Update()
        {
            var hud = GameHud.Instance;
            var input = GameInput.Instance;
            var blocked = (BattleDirector.Instance != null && BattleDirector.Instance.InBattle)
                          || Time.timeScale <= 0f || _player.IsDead;
            if (blocked)
            {
                CancelHold();
                _current = null;
                hud?.ShowPrompt(null);
                return;
            }

            if (_holding != null)
            {
                UpdateHold(input, hud);
                return;
            }

            _current = FindBest();
            hud?.ShowPrompt(_current != null ? $"[X/E] {_current.Prompt}" : null);
            if (_current == null || input == null || !input.InteractPressed || !_player.Controller.CanAct) return;

            if (_current.HoldSeconds > 0f)
            {
                _holding = _current;
                _held = 0f;
                _holdLabel = _current.Prompt?.Replace("Search ", "") ?? "";
                _player.Controller.Lock();
                _player.Controller.FaceTowards(_current.transform.position);
                _player.Controller.Animator?.SetBool(AnimParams.Searching, true);
                AudioService.Play(SoundId.Rummage);
            }
            else
            {
                _current.Interact(_player);
            }
        }

        /// <summary>Scripted use for the autopilot: interacts immediately, skipping the hold.</summary>
        public bool InteractNow(Interactable target)
        {
            if (target == null || !target.CanInteract) return false;
            target.Interact(_player);
            return true;
        }

        private void UpdateHold(GameInput input, GameHud hud)
        {
            if (input == null || !input.InteractHeld)
            {
                CancelHold();
                hud?.ShowProgress(-1f);
                return;
            }
            _held += Time.deltaTime;
            var k = _held / _holding.HoldSeconds;
            hud?.ShowProgress(Mathf.Clamp01(k));
            hud?.ShowPrompt($"Rummaging {_holdLabel}...");
            if (Mathf.Repeat(_held, 0.6f) < Time.deltaTime) AudioService.Play(SoundId.Rummage, 0.6f);
            if (k < 1f) return;
            _held = 0f;
            // Stays locked and held while InteractStage keeps asking for more (a container digging for
            // extra loot); the moment it says stop, or the player lets go of Interact above, it ends.
            if (_holding.InteractStage(_player)) return;
            CancelHold();
            hud?.ShowProgress(-1f);
        }

        private void CancelHold()
        {
            if (_holding == null) return;
            _holding = null;
            _player.Controller.Unlock();
            _player.Controller.Animator?.SetBool(AnimParams.Searching, false);
        }

        private Interactable FindBest()
        {
            Interactable best = null;
            var bestScore = float.MaxValue;
            var p = transform.position + Vector3.up * 0.8f;
            var fwd = transform.forward;
            foreach (var i in All)
            {
                if (i == null || !i.CanInteract || i.Prompt == null) continue;
                var d = i.FocusPoint - p;
                var dist = d.magnitude;
                if (dist > i.Radius) continue;
                var flat = new Vector3(d.x, 0f, d.z).normalized;
                var facing = Vector3.Dot(fwd, flat);
                if (facing < -0.2f && dist > 0.7f) continue;
                var score = dist - facing * 0.6f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}

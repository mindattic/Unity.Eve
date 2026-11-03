using UnityEngine;
using Unity.Eve.Data;
using Unity.Eve.Player;
using Unity.Eve.UI;

namespace Unity.Eve.Interaction
{
    /// <summary>An item lying in the open. A slow glint marks it, as in PE.</summary>
    public sealed class Pickup : Interactable
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;
        [SerializeField] private Light _glint;

        /// <summary>How many are still lying here.</summary>
        public int Remaining => _count;

        /// <summary>Some were taken but the bag was too full for the rest (the leftover is saved).</summary>
        public bool PartlyTaken { get; private set; }

        /// <summary>Loading a save: only this many are left here.</summary>
        public void RestoreRemaining(int remaining)
        {
            if (remaining <= 0 || remaining > _count) return;
            if (remaining < _count) PartlyTaken = true;
            _count = remaining;
        }

        public override string Prompt => _item != null ? $"Pick up {_item.Stats.DisplayName}" : null;

        public void Configure(ItemDefinition item, int count, Light glint)
        {
            _item = item;
            _count = count;
            _glint = glint;
        }

        private void Update()
        {
            if (_glint != null) _glint.intensity = 0.6f + Mathf.Sin(Time.time * 3f) * 0.4f;
        }

        public override void Interact(PlayerCharacter player)
        {
            var overflow = player.Bag.Add(_item.Id, _count);
            if (overflow == _count)
            {
                AudioService.Play(SoundId.UiDeny);
                GameHud.Instance?.ShowMessage("The bag is full.");
                return;
            }
            AudioService.Play(SoundId.ItemFound);
            var taken = _count - overflow;
            GameHud.Instance?.ShowMessage($"Got {_item.Stats.DisplayName}" + (taken > 1 ? $" x{taken}." : ".")
                + (overflow > 0 ? " The bag is full." : ""));
            // Leftovers that didn't fit stay here to pick up later, instead of vanishing with the pickup.
            if (overflow > 0) { _count = overflow; PartlyTaken = true; return; }
            gameObject.SetActive(false);
        }
    }
}

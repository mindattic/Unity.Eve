using UnityEngine;
using UnityEve.Data;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Interaction
{
    /// <summary>An item lying in the open. A slow glint marks it, as in PE.</summary>
    public sealed class Pickup : Interactable
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;
        [SerializeField] private Light _glint;

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
            GameHud.Instance?.ShowMessage($"Got {_item.Stats.DisplayName}" + (_count > 1 ? $" x{_count - overflow}." : "."));
            gameObject.SetActive(false);
        }
    }
}

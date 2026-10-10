using UnityEngine;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Interaction
{
    /// <summary>A note, sign or poster: environmental storytelling in one line.</summary>
    public sealed class Readable : Interactable
    {
        [SerializeField] private string _label = "Read note";
        [SerializeField, TextArea] private string _text = "";

        public override string Prompt => _label;

        public void Configure(string label, string text)
        {
            _label = label;
            _text = text;
        }

        public override void Interact(PlayerCharacter player)
        {
            AudioService.Play(SoundId.UiConfirm);
            GameHud.Instance?.ShowMessage(_text, 5f);
        }
    }
}

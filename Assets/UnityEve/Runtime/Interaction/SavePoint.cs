using UnityEngine;
using UnityEve.Game;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Interaction
{
    /// <summary>
    /// A payphone that still has a dial tone: use it to save. Its green handset light makes save
    /// points easy to spot from any camera. Saving also restores a little HP, a small mercy.
    /// </summary>
    public sealed class SavePoint : Interactable
    {
        [SerializeField] private string _location = "Kingsport";
        [SerializeField] private Light _lamp;

        public string Location => _location;
        public override string Prompt => "Use the payphone (save)";

        public void Configure(string location, Light lamp)
        {
            _location = location;
            _lamp = lamp;
        }

        private void Update()
        {
            if (_lamp != null) _lamp.intensity = 1.4f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.5f;
        }

        public override void Interact(PlayerCharacter player)
        {
            var battle = Battle.BattleDirector.Instance;
            if (battle != null && battle.InBattle) return;
            var save = SaveSystem.Save(this);
            if (save == null)
            {
                AudioService.Play(SoundId.UiDeny);
                GameHud.Instance?.ShowMessage("Couldn't save.", 3f);
                return;
            }
            AudioService.Play(SoundId.Save);
            GameHud.Instance?.ShowMessage($"Progress saved at {_location}. ({save.Summary})", 3f);
        }
    }
}

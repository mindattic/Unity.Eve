using System;
using System.Linq;
using UnityEngine;
using UnityEve.Battle;
using UnityEve.Core.Story;
using UnityEve.UI;

namespace UnityEve.Game
{
    /// <summary>
    /// The night's story spine for the vertical slice. Learns facts as the game is played (items
    /// picked up, weapons taken, doors tried or opened, encounters cleared), shows the current
    /// objective on the HUD and announces each new one, and calls the ending when the last falls.
    /// The Core <see cref="Objectives"/> decides what is current; this only feeds it and presents it.
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        [SerializeField] private string[] _texts = Array.Empty<string>();
        [SerializeField] private string[] _facts = Array.Empty<string>();

        private Objectives _objectives;
        private float _poll;

        public static StoryDirector Instance { get; private set; }
        public Objectives Objectives => _objectives;
        public string CurrentText => _objectives?.Current?.Text;
        public int Kills { get; set; }
        public float PlaySeconds { get; set; }
        public bool Ended { get; private set; }

        public void Configure(string[] texts, string[] facts)
        {
            _texts = texts;
            _facts = facts;
        }

        private void Awake()
        {
            Instance = this;
            _objectives = new Objectives(_texts.Select((t, i) => new Objective(t, _facts[i])));
            _objectives.Changed += OnChanged;
            _objectives.Completed += OnCompleted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var battle = BattleDirector.Instance;
            if (battle != null)
            {
                battle.BattleEnded += (zone, won) => { if (won && zone != null) Learn("cleared:" + zone.name); };
                battle.EnemyKilled += _ => Kills++;
            }
            GameHud.Instance?.SetObjective(CurrentText);
        }

        public bool Learn(string fact) => _objectives != null && _objectives.Learn(fact);

        /// <summary>Loading a save: facts come back silently, then the HUD catches up.</summary>
        public void Restore(System.Collections.Generic.IEnumerable<string> facts, int kills, float playSeconds)
        {
            _objectives.Restore(facts);
            Kills = kills;
            PlaySeconds = playSeconds;
            GameHud.Instance?.SetObjective(CurrentText);
        }

        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || session.OnTitle || Ended) return;
            PlaySeconds += Time.unscaledDeltaTime;

            // Items and weapons are facts the moment they are owned.
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0f) return;
            _poll = 0.25f;
            var player = session.Player;
            foreach (var slot in player.Bag.Slots) Learn("item:" + slot.ItemId);
            foreach (var w in player.Weapons) if (w != null) Learn("weapon:" + w.Stats.Id);
        }

        private void OnChanged(Objective next)
        {
            GameHud.Instance?.SetObjective(next.Text);
            GameHud.Instance?.ShowBanner("NEW OBJECTIVE", next.Text, 3f);
            AudioService.Play(SoundId.LevelUp, 0.6f);
        }

        private void OnCompleted()
        {
            Ended = true;
            GameHud.Instance?.SetObjective(null);
            GameSession.Instance?.Ending();
        }
    }
}

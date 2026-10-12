using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEve.Core.Util;
using UnityEve.Data;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Interaction
{
    /// <summary>
    /// Something to rummage through: a drawer, fridge, dumpster, crate. Hold Interact to search;
    /// it rolls its loot table once (or gives its fixed contents). Searched containers stay empty.
    /// </summary>
    public sealed class Container : Interactable
    {
        [SerializeField] private string _noun = "drawer";
        [SerializeField] private float _searchSeconds = 1.2f;
        [SerializeField] private LootTableDefinition _loot;
        [Tooltip("Guaranteed contents, given in addition to any loot roll (e.g. a key).")]
        [SerializeField] private List<ItemDefinition> _fixedItems = new List<ItemDefinition>();
        [SerializeField] private string _flavour;
        [SerializeField] private Light _glint;

        /// <summary>Arc Raiders-style rummaging: every <see cref="HoldSeconds"/> she keeps digging rolls
        /// one more item, up to this many stages total — release Interact any time to keep what's found.</summary>
        public const int MaxStages = 3;

        private bool _searched;
        private bool _done;
        private int _stage;

        public bool Searched => _searched;
        /// <summary>Loading a save: it was already emptied.</summary>
        public void MarkSearched()
        {
            _searched = true;
            _done = true;
        }
        public override string Prompt => _searched ? null : $"Search {_noun}";
        public override float HoldSeconds => _searchSeconds;

        public void Configure(string noun, LootTableDefinition loot, string flavour = null, params ItemDefinition[] fixedItems)
        {
            _noun = noun;
            _loot = loot;
            _flavour = flavour;
            _fixedItems = fixedItems.ToList();
        }

        /// <summary>A small amber glint marks anything still worth searching, visible before she's close
        /// enough for the prompt (the Pickup sparkle is cyan; this is its own colour so the two read apart).</summary>
        public void SetGlint(Light glint) => _glint = glint;

        private void Update()
        {
            if (_glint == null) return;
            if (_searched)
            {
                _glint.enabled = false;
                return;
            }
            _glint.intensity = 0.5f + Mathf.Sin(Time.time * 2f) * 0.3f;
        }

        public override void Interact(PlayerCharacter player)
        {
            // The autopilot's scripted InteractNow skips the hold entirely: run every stage at once so
            // it still gets everything a patient player would, in one call.
            while (InteractStage(player)) { }
        }

        public override bool InteractStage(PlayerCharacter player)
        {
            // Latched once a rummage session actually ends (naturally or via MarkSearched on load) — a
            // stray direct call (Interact()/InteractStage() outside the normal held-Interactor sequence,
            // e.g. a second InteractNow on the same container) must never roll the loot table again.
            if (_done) return false;
            _stage++;
            _searched = true;
            var found = new List<(string id, int count)>();
            if (_stage == 1) foreach (var item in _fixedItems) found.Add((item.Id, 1));
            var rolled = false;
            if (_loot != null)
            {
                var seed = (uint)(transform.position.GetHashCode() ^ System.Environment.TickCount ^ (_stage * 97));
                var roll = _loot.Table.RollOne(new Rng(seed));
                if (roll.HasValue)
                {
                    found.Add(roll.Value);
                    rolled = true;
                }
            }

            var hud = GameHud.Instance;
            if (found.Count == 0)
            {
                _done = true;
                AudioService.Play(SoundId.NothingFound);
                hud?.ShowMessage(_stage == 1 ? _flavour ?? $"Nothing useful in the {_noun}." : "Nothing more in here.");
                return false;
            }

            var parts = new List<string>();
            foreach (var (id, count) in found)
            {
                var overflow = player.Bag.Add(id, count);
                var name = player.Items.NameOf(id);
                parts.Add(count > 1 ? $"{name} x{count - overflow}" : name);
                if (overflow > 0) hud?.ShowMessage("The bag is full.");
            }
            AudioService.Play(SoundId.ItemFound);
            hud?.ShowMessage((_stage == 1 && _flavour != null ? _flavour + " " : "") + "Found: " + string.Join(", ", parts) + ".", 3.4f);

            // Keep going only while there's still a table to roll from, something was actually found this
            // stage (a dry pull means she's scraped the bottom), and the stage cap isn't reached.
            var continues = _loot != null && rolled && _stage < MaxStages;
            if (!continues) _done = true;
            return continues;
        }
    }
}

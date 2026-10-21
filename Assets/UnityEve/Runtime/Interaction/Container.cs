using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Eve.Core.Util;
using Unity.Eve.Data;
using Unity.Eve.Player;
using Unity.Eve.UI;

namespace Unity.Eve.Interaction
{
    /// <summary>
    /// Something to rummage through: a drawer, fridge, dumpster, crate. Hold Interact to search;
    /// each stage rolls its loot table once (or gives its fixed contents), up to <see cref="MaxStages"/>
    /// stages. Searched containers stay empty.
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
        /// <summary>Guaranteed items not yet delivered (bag was full when first offered). Re-offered on
        /// every later search attempt instead of being dropped, so a quest/door key is never lost for good.</summary>
        private List<ItemDefinition> _pendingFixed;

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
            _pendingFixed ??= new List<ItemDefinition>(_fixedItems);
            _stage++;
            _searched = true;
            // fixedItem is null for a loot-table roll; non-null entries are re-offered until delivered.
            var found = new List<(ItemDefinition fixedItem, string id, int count)>();
            foreach (var item in _pendingFixed) found.Add((item, item.Id, 1));
            var rolled = false;
            if (_loot != null)
            {
                var seed = (uint)(transform.position.GetHashCode() ^ System.Environment.TickCount ^ (_stage * 97));
                var roll = _loot.Table.RollOne(new Rng(seed));
                if (roll.HasValue)
                {
                    found.Add((null, roll.Value.itemId, roll.Value.count));
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
            var full = false;
            foreach (var (fixedItem, id, count) in found)
            {
                var overflow = player.Bag.Add(id, count);
                var kept = count - overflow;
                if (kept > 0)
                {
                    var name = player.Items.NameOf(id);
                    parts.Add(kept > 1 ? $"{name} x{kept}" : name);
                }
                if (overflow > 0) full = true;
                else if (fixedItem != null) _pendingFixed.Remove(fixedItem);
            }
            AudioService.Play(SoundId.ItemFound);
            // Build one message instead of two: a "bag is full" warning set above would otherwise be
            // clobbered by this call right after it, since ShowMessage has no queue.
            var message = parts.Count > 0
                ? (_stage == 1 && _flavour != null ? _flavour + " " : "") + "Found: " + string.Join(", ", parts) + "."
                : "The bag is full.";
            if (full && parts.Count > 0) message += " The bag is full.";
            if (full && _pendingFixed.Count > 0) message += " Come back for the rest once there's room.";
            hud?.ShowMessage(message, 3.4f);

            // Keep going only while the stage cap isn't reached and there's still something to chase:
            // a guaranteed item still owed, or a loot table that just paid out. Bounding on the stage cap
            // even for pending guaranteed items keeps this finite if the bag stays full the whole search.
            var continues = _stage < MaxStages && (_pendingFixed.Count > 0 || (_loot != null && rolled));
            if (!continues && _pendingFixed.Count == 0) _done = true;
            return continues;
        }
    }
}

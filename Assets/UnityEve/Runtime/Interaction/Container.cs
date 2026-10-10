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

        private bool _searched;

        public bool Searched => _searched;
        public override string Prompt => _searched ? null : $"Search {_noun}";
        public override float HoldSeconds => _searchSeconds;

        public void Configure(string noun, LootTableDefinition loot, string flavour = null, params ItemDefinition[] fixedItems)
        {
            _noun = noun;
            _loot = loot;
            _flavour = flavour;
            _fixedItems = fixedItems.ToList();
        }

        public override void Interact(PlayerCharacter player)
        {
            if (_searched) return;
            _searched = true;
            var found = new List<(string id, int count)>();
            foreach (var item in _fixedItems) found.Add((item.Id, 1));
            if (_loot != null)
            {
                var seed = (uint)(transform.position.GetHashCode() ^ System.Environment.TickCount);
                found.AddRange(_loot.Table.Roll(new Rng(seed)));
            }

            var hud = GameHud.Instance;
            if (found.Count == 0)
            {
                AudioService.Play(SoundId.NothingFound);
                hud?.ShowMessage(_flavour ?? $"Nothing useful in the {_noun}.");
                return;
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
            hud?.ShowMessage((_flavour != null ? _flavour + " " : "") + "Found: " + string.Join(", ", parts) + ".", 3.4f);
        }
    }
}

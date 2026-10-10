using System;
using System.Collections.Generic;

namespace UnityEve.UI
{
    public sealed class MenuEntry
    {
        public string Label;
        public string Detail;
        public bool Enabled = true;
        public Action OnSelect;

        public MenuEntry(string label, Action onSelect, bool enabled = true, string detail = null)
        {
            Label = label;
            OnSelect = onSelect;
            Enabled = enabled;
            Detail = detail;
        }
    }

    /// <summary>A vertical, wrapping command list. The owner feeds it input; the HUD only draws it.</summary>
    public sealed class MenuModel
    {
        public string Title;
        public readonly List<MenuEntry> Entries = new List<MenuEntry>();
        public int Cursor { get; private set; }
        public int Version { get; private set; }

        public MenuModel(string title) => Title = title;

        public MenuModel Add(MenuEntry entry)
        {
            Entries.Add(entry);
            Version++;
            return this;
        }

        public void Move(int delta)
        {
            if (Entries.Count == 0 || delta == 0) return;
            Cursor = (Cursor + delta % Entries.Count + Entries.Count) % Entries.Count;
            Version++;
            AudioService.Play(SoundId.UiMove, 0.6f);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Entries.Count) return;
            Cursor = index;
            Version++;
        }

        public MenuEntry Current => Entries.Count == 0 ? null : Entries[Cursor];

        /// <returns>True when an enabled entry was activated.</returns>
        public bool Confirm()
        {
            var entry = Current;
            if (entry == null) return false;
            if (!entry.Enabled)
            {
                AudioService.Play(SoundId.UiDeny);
                return false;
            }
            AudioService.Play(SoundId.UiConfirm);
            entry.OnSelect?.Invoke();
            return true;
        }
    }
}

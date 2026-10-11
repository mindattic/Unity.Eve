using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEve.Battle;
using UnityEve.Core.Story;
using UnityEve.Interaction;
using UnityEve.Player;

namespace UnityEve.Game
{
    /// <summary>
    /// Save points write the whole night to disk (JSON next to the player's settings); Continue
    /// reloads the scene and puts it all back: Kat's level, HP, PE, bag, weapons, the story's facts,
    /// and every container searched, item taken, door unlocked and encounter cleared, resuming at the
    /// save point. World objects are matched by their scene names.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>A save waiting to be applied once the reloaded scene starts.</summary>
        public static SaveGame Pending { get; private set; }

        /// <summary>Overrides the save file (autoplay writes to its artifacts folder).</summary>
        public static string PathOverride { get; set; }

        public static string FilePath => PathOverride ?? System.IO.Path.Combine(Application.persistentDataPath, "unity_eve_save.json");

        public static bool Exists => File.Exists(FilePath) && Read() != null;

        public static SaveGame Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var save = JsonUtility.FromJson<SaveGame>(File.ReadAllText(FilePath));
                return save != null && save.IsValid ? save : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityEve] save unreadable: {e.Message}");
                return null;
            }
        }

        public static SaveGame Save(SavePoint point)
        {
            var save = Capture(point);
            var previous = Read();
            save.Saves = (previous?.Saves ?? 0) + 1;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath) ?? ".");
            File.WriteAllText(FilePath, JsonUtility.ToJson(save, true));
            return save;
        }

        public static SaveGame Capture(SavePoint point)
        {
            var session = GameSession.Instance;
            var player = session.Player;
            var story = StoryDirector.Instance;
            var s = new SaveGame
            {
                SavePoint = point.name,
                Location = point.Location,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                PlaySeconds = story != null ? story.PlaySeconds : 0f,
                Kills = story != null ? story.Kills : 0,
                Level = player.Progression.Level,
                Exp = player.Progression.Exp,
                BonusPoints = player.Progression.BonusPoints,
                ActiveTime = player.Progression.ActiveTime,
                Hp = player.Health.Current,
                Pe = player.Pe.Current,
                Equipped = player.Weapon != null ? player.Weapon.Stats.Id : null,
                RoundsInClip = player.RoundsInClip,
            };
            foreach (var slot in player.Bag.Slots) s.Bag.Add(new SavedSlot { ItemId = slot.ItemId, Count = slot.Count });
            s.Weapons.AddRange(player.Weapons.Where(w => w != null).Select(w => w.Stats.Id));
            if (story != null) s.Facts.AddRange(story.Objectives.Facts);
            s.ClearedEncounters.AddRange(All<EncounterZone>().Where(z => z.Cleared).Select(z => z.name));
            s.SearchedContainers.AddRange(All<Container>().Where(c => c.Searched).Select(c => c.name));
            s.TakenPickups.AddRange(All<Pickup>().Where(p => !p.gameObject.activeSelf).Select(p => p.name));
            s.TakenPickups.AddRange(All<WeaponPickup>().Where(p => !p.gameObject.activeSelf).Select(p => p.name));
            s.UnlockedDoors.AddRange(All<Door>().Where(d => d.Unlocked).Select(d => d.name));
            return s;
        }

        /// <summary>Continue: reload the scene and apply the save once it has started.</summary>
        public static void Load()
        {
            var save = Read();
            if (save == null) return;
            Pending = save;
            WorldTime.Instance?.ClearAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Called by the fresh scene's GameSession: put the night back.</summary>
        public static bool ApplyPending()
        {
            var save = Pending;
            Pending = null;
            if (save == null) return false;
            var player = GameSession.Instance.Player;
            var weapons = Resources.FindObjectsOfTypeAll<Data.WeaponDefinition>();

            player.Progression.Restore(save.Level, save.Exp, save.BonusPoints, save.ActiveTime);
            player.Health.SetMax(player.Progression.MaxHpForLevel, refill: true);
            player.Pe.SetMax(player.Progression.MaxPeForLevel, refill: true);
            player.Health.Restore(save.Hp);
            player.Pe.Restore(save.Pe);
            player.Bag.Clear();
            foreach (var slot in save.Bag) player.Bag.Add(slot.ItemId, slot.Count);
            foreach (var id in save.Weapons)
            {
                var def = weapons.FirstOrDefault(w => w.Stats.Id == id);
                if (def != null) player.AddWeapon(def);
            }
            var equip = weapons.FirstOrDefault(w => w.Stats.Id == save.Equipped);
            if (equip != null) player.Equip(equip);
            player.SetRounds(save.RoundsInClip);

            foreach (var z in All<EncounterZone>()) if (save.ClearedEncounters.Contains(z.name)) z.MarkCleared();
            foreach (var c in All<Container>()) if (save.SearchedContainers.Contains(c.name)) c.MarkSearched();
            foreach (var p in All<Pickup>()) if (save.TakenPickups.Contains(p.name)) p.gameObject.SetActive(false);
            foreach (var p in All<WeaponPickup>()) if (save.TakenPickups.Contains(p.name)) p.gameObject.SetActive(false);
            foreach (var d in All<Door>()) if (save.UnlockedDoors.Contains(d.name)) d.Unlock();
            StoryDirector.Instance?.Restore(save.Facts, save.Kills, save.PlaySeconds);

            var point = All<SavePoint>().FirstOrDefault(p => p.name == save.SavePoint);
            if (point != null)
            {
                player.Controller.Teleport(point.ApproachPoint, Quaternion.LookRotation(Flat(point.transform.position - point.ApproachPoint)));
                Cameras.CameraDirector.Instance?.ResetZones();
            }
            return true;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
        }

        private static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Eve.Core.Story;
using Unity.Eve.Game;

namespace Unity.Eve.Tests
{
    /// <summary>
    /// SaveSystem's file handling against a temp folder (PathOverride): a broken save path silently
    /// loses a whole night of progress, so every way a file can be missing, corrupt or half-written is pinned.
    /// </summary>
    public class SaveSystemTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "UnityEveSaveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SaveSystem.PathOverride = Path.Combine(_dir, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.PathOverride = null;
            SetPending(null);
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        }

        private static void SetPending(SaveGame save) =>
            typeof(SaveSystem).GetProperty(nameof(SaveSystem.Pending), BindingFlags.Static | BindingFlags.Public)
                .SetValue(null, save);

        private static SaveGame Night()
        {
            var s = new SaveGame
            {
                SavePoint = "SavePoint_Church",
                Location = "Saint Ansel's",
                SavedAt = "2026-10-26 22:00",
                PlaySeconds = 845f,
                Kills = 17,
                Level = 4,
                Exp = 33,
                BonusPoints = 1,
                ActiveTime = 18,
                Hp = 97,
                Pe = 41.5f,
                Equipped = "shotgun",
                RoundsInClip = 3,
            };
            s.Bag.Add(new SavedSlot { ItemId = "medicine", Count = 2 });
            s.Bag.Add(new SavedSlot { ItemId = "shells", Count = 5 });
            s.Weapons.AddRange(new[] { "pistol", "shotgun" });
            s.Facts.AddRange(new[] { "read_mercer_letter", "has_church_key" });
            s.ClearedEncounters.Add("Encounter_Kitchen");
            s.SearchedContainers.Add("Container_Fridge");
            s.TakenPickups.Add("Pickup_ShedKey");
            s.UnlockedDoors.Add("Door_Shed");
            s.UnlockedGates.Add("Gate_Park");
            return s;
        }

        [Test]
        public void Read_returns_null_for_a_missing_file()
        {
            Assert.That(SaveSystem.Read(), Is.Null);
            Assert.That(SaveSystem.Exists, Is.False);
        }

        [Test]
        public void Read_returns_null_for_unparseable_json()
        {
            File.WriteAllText(SaveSystem.FilePath, "{ this is not json");
            Assert.That(SaveSystem.Read(), Is.Null);
            Assert.That(SaveSystem.Exists, Is.False);
        }

        [Test]
        public void Read_returns_null_for_a_wrong_version()
        {
            var s = Night();
            s.Version = SaveGame.CurrentVersion + 1;
            File.WriteAllText(SaveSystem.FilePath, JsonUtility.ToJson(s));
            Assert.That(SaveSystem.Read(), Is.Null);
        }

        [Test]
        public void Read_returns_null_for_zero_hp()
        {
            var s = Night();
            s.Hp = 0;
            File.WriteAllText(SaveSystem.FilePath, JsonUtility.ToJson(s));
            Assert.That(SaveSystem.Read(), Is.Null);
        }

        [Test]
        public void Nulled_lists_fail_validation_and_never_reach_the_game()
        {
            // JsonUtility restores a missing list as empty, so the nulled list has to be checked on the object itself.
            var s = Night();
            s.UnlockedGates = null;
            Assert.That(s.IsValid, Is.False);
            File.WriteAllText(SaveSystem.FilePath, "{\"Version\":1,\"SavePoint\":\"A\",\"Level\":1,\"Hp\":5,\"Bag\":null}");
            var read = SaveSystem.Read();
            Assert.That(read == null || read.Bag != null, Is.True, "a save never reaches the game with a null list");
        }

        [Test]
        public void A_full_night_survives_a_write_read_round_trip()
        {
            var written = SaveSystem.Write(Night());
            Assert.That(written, Is.Not.Null);
            Assert.That(SaveSystem.Exists, Is.True);

            var read = SaveSystem.Read();
            Assert.That(read, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(read), Is.EqualTo(JsonUtility.ToJson(written)));
            Assert.That(read.Bag.Count, Is.EqualTo(2));
            Assert.That(read.Bag[1].ItemId, Is.EqualTo("shells"));
            Assert.That(read.Bag[1].Count, Is.EqualTo(5));
            Assert.That(read.Weapons, Is.EqualTo(new[] { "pistol", "shotgun" }));
            Assert.That(read.Facts, Is.EqualTo(new[] { "read_mercer_letter", "has_church_key" }));
            Assert.That(read.UnlockedGates, Is.EqualTo(new[] { "Gate_Park" }));
            Assert.That(read.Pe, Is.EqualTo(41.5f));
        }

        [Test]
        public void Each_write_counts_one_more_save()
        {
            Assert.That(SaveSystem.Write(Night()).Saves, Is.EqualTo(1));
            Assert.That(SaveSystem.Write(Night()).Saves, Is.EqualTo(2));
            Assert.That(SaveSystem.Read().Saves, Is.EqualTo(2));
        }

        [Test]
        public void A_failed_write_leaves_the_previous_save_intact()
        {
            SaveSystem.Write(Night());
            var before = File.ReadAllText(SaveSystem.FilePath);

            // A directory squatting on the temp path makes the write itself fail.
            Directory.CreateDirectory(SaveSystem.FilePath + ".tmp");
            var later = Night();
            later.Level = 9;
            Assert.That(SaveSystem.Write(later), Is.Null);

            Assert.That(File.ReadAllText(SaveSystem.FilePath), Is.EqualTo(before));
            Assert.That(SaveSystem.Read().Level, Is.EqualTo(4));
        }

        [Test]
        public void ApplyPending_without_a_pending_save_does_nothing()
        {
            Assert.That(SaveSystem.ApplyPending(), Is.False);
            Assert.That(SaveSystem.Pending, Is.Null);
        }

        [Test]
        public void A_pending_save_is_consumed_by_ApplyPending_exactly_once()
        {
            SetPending(Night());
            // Edit mode has no GameSession, so applying throws, but the pending save must already be gone
            // so a broken scene can never re-apply it on the next start.
            try { SaveSystem.ApplyPending(); }
            catch (NullReferenceException) { }
            Assert.That(SaveSystem.Pending, Is.Null);
        }

        [Test]
        public void Load_without_a_save_does_not_queue_one()
        {
            SaveSystem.Load();
            Assert.That(SaveSystem.Pending, Is.Null);
        }
    }
}

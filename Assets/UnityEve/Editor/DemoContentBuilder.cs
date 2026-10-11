using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEve.Core.Battle;
using UnityEve.Core.Items;
using UnityEve.Data;

namespace UnityEve.Editor
{
    /// <summary>
    /// Builds every generated asset of the demo from code: data (weapons, items, PE, enemies, loot),
    /// synthesized audio + sound bank, URP materials, the player and creature prefabs with their
    /// animator controllers, and the Demo scene itself. Deterministic and idempotent: re-running it
    /// regenerates the same content, so the level is reviewable as code.
    /// <c>-executeMethod UnityEve.Editor.DemoContentBuilder.BuildAll</c>
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private const string Root = "Assets/UnityEve";
        private const string Art = Root + "/Art";
        private const string Gen = Root + "/Generated";
        private const string DataDir = Gen + "/Data";
        private const string MatDir = Gen + "/Materials";
        private const string AnimDir = Gen + "/Animation";
        private const string PrefabDir = Gen + "/Prefabs";
        private const string AudioDir = Gen + "/Audio";

        private sealed class Content
        {
            public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
            public readonly Dictionary<string, WeaponDefinition> Weapons = new Dictionary<string, WeaponDefinition>();
            public readonly List<AbilityDefinition> Abilities = new List<AbilityDefinition>();
            public readonly Dictionary<string, EnemyDefinition> Enemies = new Dictionary<string, EnemyDefinition>();
            public readonly Dictionary<string, LootTableDefinition> Loot = new Dictionary<string, LootTableDefinition>();
            public ItemDatabase Database;
            public SoundBank Sounds;
            public GameObject PlayerPrefab;
        }

        [MenuItem("Unity Eve/Build Demo Content")]
        public static void BuildAll()
        {
            ProjectBootstrap.Run();
            // Generated materials, clips and prefabs are rebuilt from scratch; data assets are updated in place.
            Converted.Clear();
            foreach (var dir in new[] { MatDir, AnimDir, PrefabDir }) AssetDatabase.DeleteAsset(dir);
            foreach (var dir in new[] { DataDir, MatDir, AnimDir, PrefabDir, AudioDir, Root + "/Scenes" })
                Directory.CreateDirectory(dir);

            // Re-import the models now that URP is the active pipeline, so glTFast picks URP shaders.
            AssetDatabase.ImportAsset(Art + "/Models", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            var content = new Content();
            BuildData(content);
            BuildAudio(content);
            BuildEnemyPrefabs(content);
            content.PlayerPrefab = BuildPlayerPrefab();
            AssetDatabase.SaveAssets();
            BuildScene(content);
            AssetDatabase.SaveAssets();
            Debug.Log("[UnityEve] Demo content built.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ helpers

        private static T SaveAsset<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset, true);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ItemDefinition Item(Content c, string id, string name, ItemEffect effect, int power, string description,
            int maxStack = 9, float radius = 0f, float stun = 0f, bool battle = true)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.Stats = new ItemStats
            {
                Id = id, DisplayName = name, Effect = effect, Power = power, Description = description,
                MaxStack = maxStack, Radius = radius, StunSeconds = stun, UsableInBattle = battle,
            };
            def = SaveAsset(def, $"{DataDir}/Item_{id}.asset");
            c.Items[id] = def;
            return def;
        }

        private static void BuildData(Content c)
        {
            // Items. Ammo lives in the bag like PE1, so bullets compete with medicine for slots.
            var ammo9 = Item(c, "ammo_9mm", "9mm Rounds", ItemEffect.Ammo, 10, "A magazine's worth of 9mm. Reload loads one.", 6, battle: false);
            var shells = Item(c, "ammo_shells", "Shotgun Shells", ItemEffect.Ammo, 5, "Five 12-gauge shells.", 6, battle: false);
            Item(c, "medicine", "Medicine", ItemEffect.RestoreHp, 50, "Restores 50 HP.", 5);
            Item(c, "cola", "Cola", ItemEffect.RestoreHp, 20, "Flat, warm, and somehow restores 20 HP.", 5);
            Item(c, "grenade", "Grenade", ItemEffect.Explosive, 45, "Thrown. 45 damage to everything within 3 m of the target.", 3, radius: 3f);
            Item(c, "flash", "Flash Grenade", ItemEffect.Flash, 0, "Thrown. Stuns everything within 3.5 m for 4 s.", 3, radius: 3.5f, stun: 4f);
            Item(c, "pe_tonic", "Mito Tonic", ItemEffect.RestorePe, 0, "Fully restores Parasite Energy.", 3);
            Item(c, "key_shed", "Shed Key", ItemEffect.Key, 0, "A brass key on a fish-shaped fob. 'SHED'.", 1, battle: false);

            WeaponDefinition Weapon(string id, string name, int dmg, float range, float atb, int clip, int rof, bool burst, bool melee,
                ItemDefinition ammo, SoundId sound, float root)
            {
                var w = ScriptableObject.CreateInstance<WeaponDefinition>();
                w.Stats = new WeaponStats
                {
                    Id = id, DisplayName = name, Damage = dmg, Range = range, AtbMultiplier = atb, ClipSize = clip,
                    RoundsPerTurn = rof, Burst = burst, Melee = melee, RootSeconds = root,
                };
                w.Ammo = ammo;
                w.FireSound = sound;
                w = SaveAsset(w, $"{DataDir}/Weapon_{id}.asset");
                c.Weapons[id] = w;
                return w;
            }
            // PE1 multipliers: pistol 1.0, shotgun 0.8 with burst, club 2.0.
            Weapon("pistol", "Service Pistol", 12, 7f, 1f, 10, 2, false, false, ammo9, SoundId.Gunshot, 0.45f);
            Weapon("shotgun", "Pump Shotgun", 20, 4.5f, 0.8f, 5, 1, true, false, shells, SoundId.ShotgunBlast, 0.7f);
            Weapon("pipe", "Steel Pipe", 14, 1.9f, 2f, 0, 1, false, true, null, SoundId.HitFlesh, 0.5f);

            AbilityDefinition Ability(string id, string name, AbilityEffect effect, float cost, int power, float duration, string desc, Color tint)
            {
                var a = ScriptableObject.CreateInstance<AbilityDefinition>();
                a.Stats = new AbilityStats
                {
                    Id = id, DisplayName = name, Effect = effect, PeCost = cost, Power = power, DurationSeconds = duration,
                    Description = desc, Range = 9f,
                };
                a.Tint = tint;
                a = SaveAsset(a, $"{DataDir}/PE_{id}.asset");
                c.Abilities.Add(a);
                return a;
            }
            Ability("heal", "Heal", AbilityEffect.Heal, 30, 60, 0, "Mitochondria knit the wound. Restores 60 HP.", new Color(0.4f, 1f, 0.6f));
            Ability("energy_shot", "Energy Shot", AbilityEffect.EnergyShot, 25, 34, 0, "A bolt of raw energy at one target within 9 m.", new Color(0.4f, 0.8f, 1f));
            Ability("haste", "Haste", AbilityEffect.Haste, 40, 0, 20, "The AT gauge fills twice as fast for 20 s.", new Color(1f, 0.9f, 0.3f));
            Ability("slow", "Slow", AbilityEffect.Slow, 35, 0, 15, "Creatures move and attack at half speed for 15 s.", new Color(0.7f, 0.5f, 1f));
            Ability("barrier", "Barrier", AbilityEffect.Barrier, 30, 0, 20, "Incoming damage is halved for 20 s.", new Color(0.6f, 0.9f, 1f));

            EnemyDefinition Enemy(string id, string name, int level, int hp, int def, int dmg, float speed, float reach,
                float windup, float recover, float cooldown, int exp, float aggression, float caution, float hover = 0f)
            {
                var e = ScriptableObject.CreateInstance<EnemyDefinition>();
                e.Stats = new EnemyStats
                {
                    Id = id, DisplayName = name, Level = level, MaxHp = hp, Defense = def, AttackDamage = dmg, MoveSpeed = speed,
                    Reach = reach, WindupSeconds = windup, RecoverSeconds = recover, CooldownSeconds = cooldown, Exp = exp,
                    HoverHeight = hover, Aggression = aggression, Caution = caution,
                };
                e = SaveAsset(e, $"{DataDir}/Enemy_{id}.asset");
                c.Enemies[id] = e;
                return e;
            }
            // Species tuned from ExperimentEve's playtested table; the last two numbers are temperament
            // (aggression, caution): rats swarm, spiders and serpents flank, the slime buffs and hangs back.
            Enemy("rat", "Ratchoir Straggler", 1, 26, 0, 6, 3.0f, 0.95f, 0.36f, 0.34f, 0.9f, 14, 0.85f, 0.1f);
            Enemy("spider", "Cobble Weaver", 2, 44, 1, 8, 2.3f, 1.05f, 0.42f, 0.42f, 1.0f, 24, 0.45f, 0.6f);
            Enemy("frog", "Gutterspawn", 2, 52, 2, 10, 1.7f, 1.1f, 0.52f, 0.5f, 1.2f, 28, 0.55f, 0.35f);
            Enemy("snake", "Gutter Serpent", 3, 46, 1, 13, 1.9f, 1.35f, 0.62f, 0.55f, 1.3f, 32, 0.6f, 0.55f);
            Enemy("slime", "Drain Coil", 3, 64, 3, 12, 1.1f, 1.15f, 0.72f, 0.7f, 1.5f, 38, 0.3f, 0.7f);
            Enemy("wasp", "Steeple Drone", 2, 26, 0, 9, 3.2f, 1.0f, 0.4f, 0.5f, 1.0f, 22, 0.75f, 0.25f, hover: 1.25f);
            Enemy("bat", "Steeple Bat", 1, 22, 0, 5, 3.5f, 0.95f, 0.3f, 0.4f, 0.9f, 16, 0.7f, 0.35f, hover: 1.6f);

            LootTableDefinition Loot(string id, int pulls, params (string item, int min, int max, float weight)[] entries)
            {
                var l = ScriptableObject.CreateInstance<LootTableDefinition>();
                l.Table = new LootTable
                {
                    Pulls = pulls,
                    Entries = entries.Select(e => new LootEntry { ItemId = e.item, MinCount = e.min, MaxCount = e.max, Weight = e.weight }).ToList(),
                };
                l = SaveAsset(l, $"{DataDir}/Loot_{id}.asset");
                c.Loot[id] = l;
                return l;
            }
            Loot("dumpster", 1, ("ammo_9mm", 1, 1, 3f), ("cola", 1, 1, 2f), ("", 0, 0, 2f));
            Loot("kitchen", 1, ("medicine", 1, 1, 2f), ("cola", 1, 2, 3f), ("", 0, 0, 1.5f));
            Loot("drawer", 1, ("ammo_9mm", 1, 1, 3f), ("flash", 1, 1, 1f), ("pe_tonic", 1, 1, 1f), ("", 0, 0, 2f));
            Loot("bathroom", 1, ("medicine", 1, 2, 3f), ("", 0, 0, 1f));
            Loot("garage", 2, ("grenade", 1, 1, 2f), ("ammo_shells", 1, 1, 2f), ("ammo_9mm", 1, 1, 1f));

            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            db.Items = c.Items.Values.ToList();
            c.Database = SaveAsset(db, $"{DataDir}/ItemDatabase.asset");
        }

        // ------------------------------------------------------------------ audio

        private static void BuildAudio(Content c)
        {
            WavSynth.WriteAll(AudioDir);
            AssetDatabase.ImportAsset(AudioDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            AudioClip Gen(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");
            AudioClip Ui(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Art}/Audio/ui/{name}.ogg");
            AudioClip Impact(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Art}/Audio/impact/{name}.ogg");

            var bank = ScriptableObject.CreateInstance<SoundBank>();
            void Map(SoundId id, float volume, params AudioClip[] clips) =>
                bank.Entries.Add(new SoundEntry { Id = id, Volume = volume, Clips = clips.Where(x => x != null).ToArray() });

            Map(SoundId.UiMove, 0.5f, Ui("tick_001"));
            Map(SoundId.UiConfirm, 0.7f, Ui("select_001"));
            Map(SoundId.UiCancel, 0.6f, Ui("back_001"));
            Map(SoundId.UiDeny, 0.7f, Ui("error_001"));
            Map(SoundId.AtbReady, 0.55f, Gen("atb_ready"));
            Map(SoundId.Gunshot, 0.8f, Gen("gunshot"));
            Map(SoundId.ShotgunBlast, 0.9f, Gen("shotgun"));
            Map(SoundId.Reload, 0.8f, Impact("impactMetal_light_000"), Ui("switch_001"));
            Map(SoundId.DryFire, 0.6f, Ui("click_001"));
            Map(SoundId.HitFlesh, 0.8f, Impact("impactPunch_medium_000"), Impact("impactSoft_heavy_000"));
            Map(SoundId.HitCritical, 0.9f, Impact("impactPunch_heavy_000"), Impact("impactPunch_heavy_001"));
            Map(SoundId.PlayerHurt, 0.9f, Impact("impactPunch_heavy_001"));
            Map(SoundId.EnemyTelegraph, 0.6f, Gen("telegraph"));
            Map(SoundId.EnemyDeath, 0.7f, Gen("enemy_death"));
            Map(SoundId.EnemyBuffCharge, 0.55f, Gen("enemy_buff_charge"));
            Map(SoundId.EnemyBuff, 0.7f, Gen("enemy_buff"));
            Map(SoundId.Dodge, 0.6f, Gen("dodge"));
            Map(SoundId.PerfectDodge, 0.9f, Gen("perfect_dodge"));
            Map(SoundId.PeCast, 0.7f, Gen("pe_cast"));
            Map(SoundId.Heal, 0.6f, Gen("heal"));
            Map(SoundId.Explosion, 1f, Gen("explosion"));
            Map(SoundId.Throw, 0.6f, Gen("throw"));
            Map(SoundId.DoorOpen, 0.8f, Impact("impactWood_medium_000"));
            Map(SoundId.DoorLocked, 0.8f, Impact("impactMetal_heavy_000"));
            Map(SoundId.Rummage, 0.6f, Impact("impactWood_light_000"), Impact("impactPlate_heavy_000"), Ui("scratch_001"));
            Map(SoundId.ItemFound, 0.7f, Ui("confirmation_001"));
            Map(SoundId.NothingFound, 0.6f, Ui("close_001"));
            Map(SoundId.EncounterStart, 0.8f, Gen("encounter"));
            Map(SoundId.Victory, 0.7f, Gen("victory"));
            Map(SoundId.LevelUp, 0.7f, Gen("level_up"));
            Map(SoundId.Footstep, 0.4f, Impact("footstep_concrete_000"), Impact("footstep_concrete_001"));

            c.Sounds = SaveAsset(bank, $"{DataDir}/SoundBank.asset");
            var missing = System.Enum.GetValues(typeof(SoundId)).Cast<SoundId>()
                .Where(id => id != SoundId.None && (c.Sounds.Find(id) == null || c.Sounds.Find(id).Clips.Length == 0)).ToList();
            if (missing.Count > 0) Debug.LogWarning($"[UnityEve] Silent sound ids: {string.Join(", ", missing)}");
        }
    }
}

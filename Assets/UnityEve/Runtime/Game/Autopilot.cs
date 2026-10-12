using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEve.Battle;
using UnityEve.Core.Battle;
using UnityEve.Interaction;
using UnityEve.Player;

namespace UnityEve.Game
{
    /// <summary>
    /// Self-play. Launch the player with <c>-autoplay [-artifacts DIR]</c> and it tours the demo through
    /// the real systems — cameras, rummaging, doors, an ATB battle with the range dome, a grenade, a
    /// Focus window — writing a screenshot at each beat, then quits with PASS/FAIL. "Done is verified by
    /// play": look at the PNGs.
    /// <c>-demo</c> runs the same tour windowed, with no screenshots and no PASS/FAIL, and loops forever:
    /// at the ending it clicks through to the title and plays the whole night again. Meant to be watched,
    /// unattended, on a show floor or for capturing trailer footage.
    /// </summary>
    public sealed class Autopilot : MonoBehaviour
    {
        private string _dir;
        private int _shot;
        private int _failures;
        private bool _demo;
        private PlayerCharacter _player;
        private Vector3? _retreatTarget;
        private float _retreatTargetAt;

        private static bool Autoplay => Environment.GetCommandLineArgs().Contains("-autoplay");
        private static bool Demo => Environment.GetCommandLineArgs().Contains("-demo");
        public static bool Requested => Autoplay || Demo;

        private static Autopilot _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Requested || _running != null) return;
            _running = new GameObject("Autopilot").AddComponent<Autopilot>();
            // Survives the scene reload that loading a save performs.
            DontDestroyOnLoad(_running.gameObject);
        }

        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            _demo = Demo;
            var i = Array.IndexOf(args, "-artifacts");
            _dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.dataPath, "..", "artifacts", "autoplay");
            // ScreenCapture resolves relative paths against the _Data folder, so pin an absolute path.
            _dir = Path.GetFullPath(_dir);
            Directory.CreateDirectory(_dir);
            Log($"writing to {Path.GetFullPath(_dir)}");
        }

        private static void Log(string message) => Debug.Log($"[Autopilot] {message}");

        /// <summary>
        /// In batch mode (no window) the HUD panel renders into this texture instead of the screen.
        /// The HUD already composites the PS1 render target, so one readback captures the whole frame.
        /// </summary>
        private RenderTexture _offscreen;

        private void SetupOffscreen()
        {
            var doc = FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            if (doc == null) return;
            _offscreen = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            if (UI.GameHud.Instance != null) UI.GameHud.Instance.RenderSizeOverride = new Vector2Int(1600, 900);
            var panel = Instantiate(doc.panelSettings);
            panel.targetTexture = _offscreen;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            doc.panelSettings = panel;
            Log("offscreen capture via panel render texture");
        }

        private IEnumerator Shot(string name)
        {
            // Demo mode is watched live, not verified from PNGs afterward: skip the capture and its
            // forced camera renders, keeping just a frame of pacing where a shot would have been taken.
            if (_demo)
            {
                yield return null;
                yield break;
            }
            // A batch-mode player draws UI but does not render cameras by itself: render the world
            // camera into the PS1 target explicitly, let the HUD panel composite it, then read back.
            if (_offscreen != null && Cameras.CameraDirector.Instance != null)
            {
                Cameras.CameraDirector.Instance.Camera.Render();
                yield return null;
                Cameras.CameraDirector.Instance.Camera.Render();
            }
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(_dir, $"{++_shot:00}_{name}.png");
            if (_offscreen != null)
            {
                var previous = RenderTexture.active;
                RenderTexture.active = _offscreen;
                var tex = new Texture2D(_offscreen.width, _offscreen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, _offscreen.width, _offscreen.height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex);
            }
            else
            {
                ScreenCapture.CaptureScreenshot(path);
            }
            Log($"shot {path}");
            yield return null;
            yield return null;
        }

        private static WaitForSecondsRealtime Real(float s) => new WaitForSecondsRealtime(s);

        private void Teleport(Transform at)
        {
            _player.Controller.Teleport(at.position, at.rotation);
            Cameras.CameraDirector.Instance?.ResetZones();
        }

        private void Teleport(Vector3 position, Vector3 lookAt)
        {
            var dir = lookAt - position;
            dir.y = 0f;
            _player.Controller.Teleport(position, Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.forward));
            Cameras.CameraDirector.Instance?.ResetZones();
        }

        private bool IsNear(Transform spot, float metres = 2f)
        {
            var d = _player.transform.position - spot.position;
            d.y = 0f;
            return d.magnitude < metres;
        }

        private IEnumerator Start()
        {
            if (Application.isBatchMode) SetupOffscreen();
            yield return Real(1.5f);
            GameSession session;
            do
            {
                session = GameSession.Instance;
                if (session == null)
                {
                    Log("FAIL no GameSession in scene");
                    if (!_demo) Application.Quit(2);
                    yield break;
                }
                _player = session.Player;
                _failures = 0;
                // Never touch a player's real save: autoplay saves into its artifacts folder.
                SaveSystem.PathOverride = Path.Combine(_dir, "save.json");
                if (File.Exists(SaveSystem.PathOverride)) File.Delete(SaveSystem.PathOverride);
                // Scripted steps teleport between screens; random encounters only run in their own step.
                if (ScreenEncounters.Instance != null) ScreenEncounters.Instance.Enabled = false;

                yield return RunTour(session);

                Log(_failures == 0 ? "PASS autoplay complete" : $"FAIL {_failures} step(s) failed");
                yield return Real(0.5f);
                if (!_demo)
                {
                    Application.Quit(_failures == 0 ? 0 : 1);
                    yield break;
                }

                // Attract mode: ride the ending's own "Back to the title" reset into another lap. A lap
                // that never reached the ending (a fight timed out, a check failed) has no such menu to
                // click, so force a clean reload directly — the loop must never run a second lap on
                // stale, corrupted world state (a battle stuck "in progress" forever, an untouched save,
                // a story director mid-objective), which cascades into failures and eventually a crash.
                yield return Real(2f);
                var before = GameSession.Instance;
                if (before.OnEndScreen && before.AutoChoose("Back"))
                {
                    var deadline = Time.realtimeSinceStartup + 10f;
                    while ((GameSession.Instance == null || GameSession.Instance == before) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                if (GameSession.Instance == before) before.ForceReload();
                yield return WaitForReload(before);
            } while (_demo);
        }

        private IEnumerator RunTour(GameSession session)
        {
            // The title screen renders (players see it first; autoplay skips it otherwise).
            session.ShowTitle();
            yield return Real(0.6f);
            yield return Shot("title");
            session.StartGame();
            yield return Real(0.5f);
            yield return Shot("street_start");

            // 1. Camera tour: every "Tour_" marker, in name order. Encounters stay dormant while sightseeing.
            BattleDirector.Instance.SuppressEncounters = true;
            foreach (var marker in FindObjectsByType<Transform>(FindObjectsSortMode.None)
                         .Where(t => t.name.StartsWith("Tour_")).OrderBy(t => t.name))
            {
                Teleport(marker);
                yield return Real(0.8f);
                yield return Shot(marker.name.Substring(5).ToLowerInvariant());
            }

            BattleDirector.Instance.SuppressEncounters = false;

            // 2. Rummage a street container.
            var streetBin = FindObjectsByType<Container>(FindObjectsSortMode.None).OrderBy(c => c.name).FirstOrDefault(c => c.name.Contains("Street"));
            if (streetBin != null)
            {
                Teleport(streetBin.transform.position + streetBin.transform.forward * 1.1f, streetBin.transform.position);
                yield return Real(0.6f);
                _player.GetComponent<Interactor>().InteractNow(streetBin);
                yield return Real(0.3f);
                yield return Shot("rummage_street");
            }

            // 3. Battles: walk into each encounter and fight it through the command menu.
            // (The boss waits for the story run at the end.)
            foreach (var zone in FindObjectsByType<EncounterZone>(FindObjectsSortMode.None)
                         .Where(z => !z.Roaming && !z.name.Contains("Boss")).OrderBy(z => EncounterNumber(z.name)))
            {


                Teleport(zone.Bounds.center - new Vector3(0f, zone.Bounds.extents.y - 0.1f, 0f), zone.Bounds.center + Vector3.forward);
                yield return Real(0.3f);
                var battle = BattleDirector.Instance;
                if (!battle.InBattle)
                {
                    Log($"FAIL battle did not start in {zone.name}");
                    _failures++;
                    continue;
                }
                yield return Real(1.4f);
                yield return Shot($"{zone.name.ToLowerInvariant()}_encounter");
                yield return FightToTheEnd(battle, zone.name.ToLowerInvariant());
                if (!zone.Cleared)
                {
                    Log($"FAIL {zone.name} not cleared");
                    _failures++;
                }
                yield return Real(0.5f);
            }

            // 3b. Returning to a screen after going somewhere far can start a random encounter;
            //     stepping back and forth between neighbouring screens never does.
            yield return ReentryEncounter(() => _failures++);

            // 4. The shed is padlocked until the key from the Mercer side table is in the bag.
            var shedDoor = FindObjectsByType<Door>(FindObjectsSortMode.None).FirstOrDefault(d => d.name == "Door_Shed_Enter");
            if (shedDoor == null)
            {
                Log("FAIL no Door_Shed_Enter in scene");
                _failures++;
            }
            else
            {
                Teleport(shedDoor.ApproachPoint, shedDoor.transform.position);
                yield return Real(0.4f);
                shedDoor.Interact(_player);
                yield return Real(1.2f);
                yield return Shot("shed_locked");
                if (!shedDoor.Locked || IsNear(shedDoor.Destination))
                {
                    Log("FAIL shed opened without the key");
                    _failures++;
                }
            }

            // 5. Enter the house, search everything inside, including the side table with the shed key.
            var houseDoor = FindObjectsByType<Door>(FindObjectsSortMode.None).FirstOrDefault(d => d.name == "Door_HouseA_Enter");
            if (houseDoor != null)
            {
                Teleport(houseDoor.transform.position + houseDoor.transform.forward * 1.2f, houseDoor.transform.position);
                yield return Real(0.4f);
                yield return Shot("front_door");
                houseDoor.Interact(_player);
                yield return Real(1.8f);
                yield return Shot("interior");
                var inside = FindObjectsByType<Container>(FindObjectsSortMode.None)
                    .Where(c => c.name.Contains("House") && !c.Searched).OrderBy(c => c.name).Take(3).ToList();
                var sideTable = FindObjectsByType<Container>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Container_House_SideTable");
                if (sideTable != null && !inside.Contains(sideTable)) inside.Add(sideTable);
                foreach (var c in inside)
                {
                    Teleport(c.transform.position + c.transform.forward * 1.0f, c.transform.position);
                    yield return Real(0.5f);
                    _player.GetComponent<Interactor>().InteractNow(c);
                    yield return Real(0.25f);
                    yield return Shot($"search_{c.name.ToLowerInvariant()}");
                }
            }

            // 6. With the key in the bag the shed opens; take the shotgun off the workbench.
            if (shedDoor != null)
            {
                if (_player.Bag.CountOf("key_shed") == 0)
                {
                    Log("FAIL side table did not give key_shed");
                    _failures++;
                }
                Teleport(shedDoor.ApproachPoint, shedDoor.transform.position);
                yield return Real(0.4f);
                shedDoor.Interact(_player);
                yield return Real(1.9f);
                yield return Shot("shed_interior");
                if (shedDoor.Locked || !IsNear(shedDoor.Destination))
                {
                    Log("FAIL shed did not open with the key");
                    _failures++;
                }
                var shotgun = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None).FirstOrDefault();
                if (shotgun != null)
                {
                    Teleport(shotgun.ApproachPoint, shotgun.transform.position);
                    yield return Real(0.5f);
                    _player.GetComponent<Interactor>().InteractNow(shotgun);
                    yield return Real(0.3f);
                    yield return Shot("shed_shotgun");
                    if (!_player.Weapons.Any(w => w != null && w.Stats.Id == "shotgun"))
                    {
                        Log("FAIL shotgun pickup did not add the weapon");
                        _failures++;
                    }
                }
            }

            // 7. Out of combat, walking plays the walk/jog cycle (not a sliding idle).
            var shedSpawn = GameObject.Find("Spawn_ShedInside");
            var input = Input.GameInput.Instance;
            if (shedSpawn != null && input != null && _player.Controller.Animator != null)
            {
                Teleport(shedSpawn.transform);
                yield return Real(0.3f);
                input.MoveOverride = new Vector2(0f, 1f);
                yield return Real(0.6f);
                var moving = _player.Controller.Animator.GetCurrentAnimatorClipInfo(0)
                    .Where(ci => ci.weight > 0.3f).Any(ci => ci.clip != null && (ci.clip.name.Contains("Walk") || ci.clip.name.Contains("Jog")));
                yield return Shot("explore_walk");
                input.MoveOverride = null;
                if (!moving)
                {
                    Log("FAIL explore locomotion is not playing a walk/jog clip");
                    _failures++;
                }
            }

            // 8. Field menu.
            session.OpenFieldMenu();
            yield return Real(0.3f);
            yield return Shot("field_menu");
            session.CloseMenu();

            // 9. The vertical slice: church barred, the grocery's keys, save and load, the Mother, the ending.
            yield return StorySlice(() => _failures++);
        }

        private static int EncounterNumber(string name)
        {
            var digits = new string(name.SkipWhile(ch => !char.IsDigit(ch)).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) ? n : 999;
        }

        private static T Named<T>(string name) where T : Component =>
            FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == name);

        private IEnumerator UseDoor(string name, string shot)
        {
            var door = Named<Door>(name);
            if (door == null) yield break;
            Teleport(door.ApproachPoint, door.transform.position);
            yield return Real(0.4f);
            door.Interact(_player);
            yield return Real(2.2f);
            if (shot != null) yield return Shot(shot);
        }

        private IEnumerator Search(string name)
        {
            var c = Named<Container>(name);
            if (c == null) yield break;
            Teleport(c.ApproachPoint, c.transform.position);
            yield return Real(0.4f);
            _player.GetComponent<Interactor>().InteractNow(c);
            yield return Real(0.5f);
        }

        private IEnumerator StorySlice(Action fail)
        {
            void Check(bool ok, string what)
            {
                if (ok) Log($"story: {what}");
                else
                {
                    Log($"FAIL story: {what}");
                    fail();
                }
            }
            var story = StoryDirector.Instance;
            Check(story != null && story.Objectives.Knows("weapon:shotgun"), "shed key and shotgun recorded as facts");
            Log($"objective: {story?.CurrentText}");

            // The church door is barred and points the way to the grocery.
            yield return UseDoor("Door_Church_Enter", "church_barred");
            Check(story.Objectives.Knows("flag:church_tried") && story.CurrentText.Contains("Kingsport Foods"), "barred church sends Kat to Kingsport Foods");

            // Kingsport Foods: the register holds the storeroom key, the manager's desk the church key.
            yield return UseDoor("Door_Grocery_Enter", "grocery_inside");
            yield return Search("Container_Grocery_Register");
            Check(_player.Bag.CountOf("key_storeroom") > 0, "storeroom key from the register");
            yield return UseDoor("Door_Storeroom_Enter", "storeroom_unlocked");
            yield return Search("Container_Storeroom_Desk");
            yield return Shot("church_key");
            Check(_player.Bag.CountOf("key_church") > 0, "church key from the manager's desk");
            Log($"objective: {story.CurrentText}");

            // Save at the store's payphone, then load: the scene reloads and everything comes back.
            var phone = Named<SavePoint>("SavePoint_Grocery");
            Teleport(phone.ApproachPoint, phone.transform.position);
            yield return Real(0.5f);
            _player.GetComponent<Interactor>().InteractNow(phone);
            yield return Real(0.6f);
            yield return Shot("saved");
            var expectLevel = _player.Progression.Level;
            var expectFacts = story.Objectives.Facts.Count;
            var expectSlots = _player.Bag.Slots.Count;
            Check(File.Exists(SaveSystem.PathOverride) && SaveSystem.Read() != null, "save written");

            var oldSession = GameSession.Instance;
            SaveSystem.Load();
            yield return WaitForReload(oldSession);
            story = StoryDirector.Instance;
            yield return Real(0.5f);
            yield return Shot("loaded");
            Check(_player.Progression.Level == expectLevel, $"level restored ({expectLevel})");
            Check(story.Objectives.Facts.Count == expectFacts, $"story facts restored ({expectFacts})");
            Check(_player.Bag.Slots.Count == expectSlots && _player.Bag.CountOf("key_church") > 0, "bag restored with the church key");
            Check(Named<Container>("Container_Grocery_Register").Searched, "register stays searched");
            Check(Named<Door>("Door_Storeroom_Enter").Unlocked, "storeroom door stays unlocked");
            Check(Vector3.Distance(_player.transform.position, Named<SavePoint>("SavePoint_Grocery").ApproachPoint) < 2f, "resumed at the payphone");

            // Game over: fall in a fight, see the menu, Continue back to the last save.
            yield return GameOverAndContinue(Check);
            story = StoryDirector.Instance;

            // Saint Ansel's: in through the side door, into the nave, and the Mother.
            yield return UseDoor("Door_Church_Enter", "nave");
            Check(story.Objectives.Knows("flag:church_entered"), "inside Saint Ansel's");
            var boss = Named<EncounterZone>("Encounter_Boss_Nave");
            var battle = BattleDirector.Instance;
            Teleport(boss.Bounds.center - new Vector3(0f, boss.Bounds.extents.y - 0.1f, 3f), boss.Bounds.center + Vector3.forward * 4f);
            yield return Real(0.4f);
            Check(battle.InBattle && battle.BossFight, "the boss fight begins");
            yield return Real(0.3f);
            Check(MusicDirector.Instance != null && MusicDirector.Instance.Playing == "boss", "boss music plays");
            yield return Real(1.6f);
            yield return Shot("boss_encounter");
            // Demo mode plays safety-first (retreats, leans on cover) rather than for speed, so the boss
            // can legitimately take longer than autoplay's scripted, all-offence run; give it more room.
            yield return FightToTheEnd(battle, "boss", _demo ? 480f : 240f);
            Check(boss.Cleared, "the Mother is dead");
            yield return Real(6f);
            yield return Shot("ending");
            Check(story.Ended && GameSession.Instance.OnEndScreen, "the ending plays");
        }

        private IEnumerator WaitForReload(GameSession old)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while ((GameSession.Instance == null || GameSession.Instance == old || SaveSystem.Pending != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Real(1.2f);
            if (Application.isBatchMode) SetupOffscreen();
            if (ScreenEncounters.Instance != null) ScreenEncounters.Instance.Enabled = false;
            _player = GameSession.Instance.Player;
        }

        private IEnumerator GameOverAndContinue(Action<bool, string> check)
        {
            // Start a fight the honest way (a qualifying return to a screen), then fall in it.
            var screens = ScreenEncounters.Instance;
            var west = Marker("Tour_01_street_west");
            var cctv = Marker("Tour_03_street_cctv");
            var far = Marker("Tour_10_shed");
            Teleport(cctv);
            yield return Real(0.4f);
            Teleport(west);
            yield return Real(0.4f);
            screens.Enabled = true;
            screens.Rule.Chance = 1f;
            screens.Rule.Cooldown = 0f;
            Teleport(far);
            yield return Real(0.5f);
            Teleport(west);
            yield return Real(1.2f);
            var battle = BattleDirector.Instance;
            check(battle.InBattle, "a fight to fall in");
            check(MusicDirector.Instance != null && MusicDirector.Instance.Playing == "battle", "battle music plays");
            screens.Enabled = false;
            _player.TakeDamage(99999);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (GameSession.Instance.CurrentMenu == null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Real(0.4f);
            yield return Shot("game_over");
            var old = GameSession.Instance;
            check(old.CurrentMenu != null && old.AutoChoose("Continue"), "game over offers Continue");
            yield return WaitForReload(old);
            yield return Shot("continued");
            check(!_player.IsDead && Vector3.Distance(_player.transform.position, Named<SavePoint>("SavePoint_Grocery").ApproachPoint) < 2f,
                "continued from the grocery payphone");
            check(MusicDirector.Instance != null && MusicDirector.Instance.Playing == "explore", "exploration music after the reload");
        }

        private Transform Marker(string name) => FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        private IEnumerator ReentryEncounter(Action fail)
        {
            var screens = ScreenEncounters.Instance;
            var west = Marker("Tour_01_street_west");
            var cctv = Marker("Tour_03_street_cctv");
            var far = Marker("Tour_10_shed"); // an interior: far from the street, hosts no encounters itself
            if (screens == null || west == null || cctv == null || far == null)
            {
                Log("FAIL re-entry encounter: missing screens");
                fail();
                yield break;
            }
            var battle = BattleDirector.Instance;
            // Settle on the street first (still disabled) so earlier tours do not count as trips away.
            Teleport(cctv);
            yield return Real(0.4f);
            Teleport(west);
            yield return Real(0.4f);
            screens.Enabled = true;
            screens.Rule.Chance = 1f;
            screens.Rule.Cooldown = 0f;

            Teleport(cctv); // neighbour
            yield return Real(0.5f);
            Teleport(west); // straight back: must stay quiet
            yield return Real(0.6f);
            if (battle.InBattle)
            {
                Log("FAIL re-entry encounter: bouncing between neighbouring screens started a battle");
                fail();
                yield return FightToTheEnd(battle, "reentry_bounce");
            }

            Teleport(far); // far away
            yield return Real(0.5f);
            Teleport(west); // a qualifying return
            yield return Real(0.8f);
            if (!battle.InBattle)
            {
                Log("FAIL re-entry encounter: returning from a far screen did not start a battle");
                fail();
            }
            else
            {
                yield return Real(1.2f);
                yield return Shot("reentry_encounter");
                yield return FightToTheEnd(battle, "reentry");
            }
            screens.Rule.Chance = Core.Battle.BattleConfig.ScreenEncounterChance;
            screens.Rule.Cooldown = Core.Battle.BattleConfig.ScreenEncounterCooldownSeconds;
            screens.Enabled = false;
        }

        private IEnumerator FightToTheEnd(BattleDirector battle, string tag, float seconds = 90f)
        {
            var turn = 0;
            var focusShown = false;
            var buffShown = false;
            var chargeShown = false;
            var shootdodgeShown = false;
            var intents = new System.Collections.Generic.HashSet<EnemyIntent>();
            var timeout = Time.realtimeSinceStartup + seconds;
            while (battle.InBattle && battle.Phase != BattlePhase.Victory && Time.realtimeSinceStartup < timeout)
            {
                // The autopilot is a tourist, not a test of skill: keep it alive.
                if (_player.Health.Fraction < 0.5f) _player.Heal(999);

                foreach (var e in battle.Enemies.Where(e => e.IsAlive)) intents.Add(e.Intent);
                if (!chargeShown && battle.Phase == BattlePhase.Active && battle.Enemies.Any(e => e.IsAlive && e.RearingUp))
                {
                    chargeShown = true;
                    yield return Shot($"{tag}_enemy_rears_up");
                    yield return Real(0.45f);
                    yield return Shot($"{tag}_enemy_charges");
                }
                if (!buffShown && battle.Phase == BattlePhase.Active && battle.Enemies.Any(e => e.IsAlive && e.Intent == EnemyIntent.Buff))
                {
                    buffShown = true;
                    yield return Real(0.5f);
                    yield return Shot($"{tag}_enemy_buff");
                }

                if (_demo && battle.Phase == BattlePhase.Active)
                {
                    // Survival first: keep distance and lean on cover. A real Cancel-button dodge is
                    // also attempted every tick, timed against a committed strike through the same input
                    // path a human uses — it lands as an honest perfect dodge if the timing works out.
                    TryStaySafe(battle);
                    TryRollDodge(battle);
                }

                if (!focusShown && battle.Phase == BattlePhase.Active)
                {
                    if (_demo)
                    {
                        if (battle.Focus.IsOpen)
                        {
                            focusShown = true;
                            yield return Shot($"{tag}_focus");
                            battle.UseFocusMove(FocusMove.TargetVitals);
                            yield return Real(0.3f);
                            yield return Shot($"{tag}_vitals");
                        }
                    }
                    else if (turn == 1 && battle.DebugOpenFocus())
                    {
                        focusShown = true;
                        yield return Real(0.5f);
                        yield return Shot($"{tag}_focus");
                        battle.UseFocusMove(FocusMove.TargetVitals);
                        yield return Real(0.3f);
                        yield return Shot($"{tag}_vitals");
                    }
                }

                // Active reload: press Confirm again the instant the bar enters the sweet spot, through
                // the same input path a human uses, shortening the reload like a real timed press would.
                if (_demo && battle.CurrentReload != null && battle.CurrentReload.InSweetSpot)
                    Input.GameInput.Instance?.TriggerConfirm();

                // Shootdodge: real time, not ATB-gated, so it's exercised here rather than in the
                // command-menu turn chain below. TriggerShootdodge/TriggerConfirm press the real inputs so
                // the whole leap-fire-land sequence runs through the same path a human's does.
                if (!shootdodgeShown && !_demo && battle.Phase == BattlePhase.Active && turn == 6 &&
                    _player.Weapon != null && !_player.Weapon.Stats.Melee)
                {
                    shootdodgeShown = true;
                    Input.GameInput.Instance?.TriggerShootdodge();
                    yield return Real(0.08f);
                    if (_player.Controller.IsShootdodging)
                    {
                        yield return Shot($"{tag}_shootdodge");
                        for (var i = 0; i < 3 && _player.Controller.IsShootdodging; i++)
                        {
                            Input.GameInput.Instance?.TriggerConfirm();
                            yield return Real(0.15f);
                        }
                        while (_player.Controller.IsShootdodging) yield return null;
                        yield return Shot($"{tag}_shootdodge_land");
                    }
                }

                if (battle.Phase != BattlePhase.Active || !battle.Atb.IsFull)
                {
                    yield return null;
                    continue;
                }

                battle.OpenCommandMenu();
                yield return Real(0.25f);
                if (turn == 0) yield return Shot($"{tag}_command_menu");

                var alive = battle.Enemies.Count(e => e.IsAlive);
                var hasGrenade = _player.Bag.CountOf("grenade") > 0;
                if (alive >= 2 && hasGrenade && turn == 1)
                {
                    battle.AutoChoose("Item");
                    yield return Real(0.2f);
                    yield return Shot($"{tag}_item_menu");
                    battle.AutoChoose("Grenade");
                    yield return Real(0.3f);
                    if (battle.AutoConfirmTarget())
                    {
                        yield return Real(0.85f);
                        yield return Shot($"{tag}_grenade");
                    }
                    else
                    {
                        battle.AutoCancel(); // nothing within throwing range
                    }
                }
                else if (turn == 2 && _player.Pe.CanAfford(BattleConfig.KatanaRushPeCost))
                {
                    // Katana Rush: dash through a target, cutting everything on the way.
                    battle.AutoChoose("Blade");
                    yield return Real(0.2f);
                    yield return Shot($"{tag}_blade_menu");
                    battle.AutoChoose("Rush");
                    yield return Real(0.3f);
                    if (battle.Phase == BattlePhase.Targeting)
                    {
                        yield return Shot($"{tag}_rush_dome");
                        battle.AutoConfirmTarget();
                        yield return Real(0.12f);
                        yield return Shot($"{tag}_rush");
                        yield return Real(0.5f);
                    }
                }
                else if (turn == 4 && _player.Pe.CanAfford(BattleConfig.KatanaRushPeCost))
                {
                    // Line Rush: the hand-aimed dash. AutoConfirmAim aims at the nearest enemy and
                    // confirms at once, standing in for a human rotating the stick then pressing Confirm.
                    battle.AutoChoose("Blade");
                    yield return Real(0.2f);
                    yield return Shot($"{tag}_blade_menu2");
                    battle.AutoChoose("Line Rush");
                    yield return Real(0.3f);
                    if (battle.Phase == BattlePhase.Aiming)
                    {
                        yield return Shot($"{tag}_line_rush_aim");
                        battle.AutoConfirmAim();
                        yield return Real(0.12f);
                        yield return Shot($"{tag}_line_rush");
                        yield return Real(0.5f);
                    }
                }
                else if (turn == 3 || (_player.NeedsReload && !_player.CanReload))
                {
                    // Out of bullets with nothing to reload: the katana still works.
                    // Katana Slash: step up to the nearest creature and cut.
                    var nearest = battle.Enemies.Where(e => e.IsAlive)
                        .OrderBy(e => (e.transform.position - _player.transform.position).sqrMagnitude).FirstOrDefault();
                    if (nearest != null)
                    {
                        var to = nearest.transform.position - _player.transform.position;
                        to.y = 0f;
                        _player.Controller.Teleport(nearest.transform.position - to.normalized * 1.3f, Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to : Vector3.forward));
                    }
                    battle.AutoChoose("Blade");
                    yield return Real(0.15f);
                    battle.AutoChoose("Slash");
                    yield return Real(0.25f);
                    if (battle.Phase == BattlePhase.Targeting)
                    {
                        battle.AutoConfirmTarget();
                        yield return Real(0.22f);
                        yield return Shot($"{tag}_slash");
                        yield return Real(0.4f);
                    }
                }
                else if (_player.NeedsReload)
                {
                    battle.AutoChoose("Reload");
                }
                else
                {
                    battle.AutoChoose("Attack");
                    yield return Real(0.35f);
                    if (battle.Phase == BattlePhase.Targeting)
                    {
                        if (turn == 0) yield return Shot($"{tag}_range_dome");
                        battle.AutoConfirmTarget();
                        yield return Real(0.2f);
                        if (turn == 0) yield return Shot($"{tag}_attack");
                    }
                    else
                    {
                        // Nothing in range: step toward the nearest enemy and try again.
                        var nearest = battle.Enemies.Where(e => e.IsAlive)
                            .OrderBy(e => (e.transform.position - _player.transform.position).sqrMagnitude).FirstOrDefault();
                        battle.AutoCancel();
                        if (nearest != null)
                            _player.Controller.Teleport(Vector3.MoveTowards(_player.transform.position, nearest.transform.position, 2.5f), _player.transform.rotation);
                    }
                }
                if (battle.Phase == BattlePhase.Command || battle.Phase == BattlePhase.Targeting) battle.AutoCancel();
                turn++;
                yield return null;
            }
            Log($"{tag} enemy intents seen: {string.Join(", ", intents.OrderBy(i => i))}");
            yield return Real(0.4f);
            if (battle.Phase == BattlePhase.Victory) yield return Shot($"{tag}_victory");
            while (battle.InBattle && Time.realtimeSinceStartup < timeout) yield return null;
        }

        /// <summary>Demo mode: a real Cancel-button press, timed against whichever enemy is about to land
        /// its strike, through the same input path a human dodge uses (see GameInput.TriggerDodge).</summary>
        private bool TryRollDodge(BattleDirector battle)
        {
            var incoming = battle.Enemies
                .Where(e => e.IsAlive && e.SecondsUntilStrike <= FocusWindow.PerfectWindowSeconds - 0.08f)
                .OrderBy(e => e.SecondsUntilStrike)
                .FirstOrDefault();
            if (incoming == null) return false;
            Input.GameInput.Instance?.TriggerDodge();
            return true;
        }

        /// <summary>Demo mode's #1 goal is staying alive, not winning fast: keep distance from whoever is
        /// closest, up to the current weapon's effective range, and gravitate toward a nearby cover prop
        /// when one is in reach.</summary>
        private void TryStaySafe(BattleDirector battle)
        {
            var nearest = battle.Enemies.Where(e => e.IsAlive)
                .OrderBy(e => (e.transform.position - _player.transform.position).sqrMagnitude).FirstOrDefault();
            if (nearest == null)
            {
                _retreatTarget = null;
                return;
            }
            var pos = _player.transform.position;
            var away = pos - nearest.transform.position;
            away.y = 0f;
            var dist = away.magnitude;
            var weapon = _player.Weapon;
            var optimum = weapon != null && !weapon.Stats.Melee ? weapon.Stats.Range * 0.85f : 3f;
            if (dist >= optimum * 0.9f)
            {
                _retreatTarget = null;
                return;
            }

            // Pick a retreat point and hold it for a beat: re-picking every single frame flickers between
            // "duck behind cover" and "step straight back" as the enemy closes in, and shakes her in place.
            if (_retreatTarget == null || Time.time >= _retreatTargetAt)
            {
                var dir = dist > 0.1f ? away / dist : _player.transform.forward;
                var desired = pos + dir * 1.2f;
                var cover = NearestCoverProp(desired);
                var target = cover != null && (cover.position - desired).sqrMagnitude < 9f
                    ? cover.position + (cover.position - nearest.transform.position).normalized * 0.7f
                    : desired;
                var zone = battle.Zone;
                if (zone != null)
                {
                    var b = zone.Bounds;
                    target.x = Mathf.Clamp(target.x, b.min.x + 0.6f, b.max.x - 0.6f);
                    target.z = Mathf.Clamp(target.z, b.min.z + 0.6f, b.max.z - 0.6f);
                }
                _retreatTarget = target;
                _retreatTargetAt = Time.time + 0.5f;
            }

            const float retreatSpeed = 4f; // matches PlayerController's default run speed
            var toTarget = _retreatTarget.Value - pos;
            toTarget.y = 0f;
            var remaining = toTarget.magnitude;
            if (remaining < 0.02f) return; // already there

            var moveDir = toTarget / remaining;
            // Obstacle-aware: this moves her with a direct Teleport each tick, not the CharacterController,
            // so nothing naturally stops her walking into a bench or lamp post on the way — steer around
            // whatever a short sphere-cast finds directly ahead instead.
            if (Physics.SphereCast(pos + Vector3.up * 0.9f, 0.35f, moveDir, out var hit, 1.1f) && !hit.collider.isTrigger)
            {
                var around = Vector3.Cross(Vector3.up, moveDir);
                var outward = hit.normal;
                outward.y = 0f;
                if (outward.sqrMagnitude > 0.0001f && Vector3.Dot(around, outward) < 0f) around = -around;
                moveDir = (moveDir + around).normalized;
            }
            var step = pos + moveDir * Mathf.Min(remaining, retreatSpeed * Time.deltaTime);
            // Already there — boxed against the arena edge with nowhere left to go: stop nudging her.
            if ((step - pos).sqrMagnitude < 0.0004f) return;

            var face = nearest.transform.position - step;
            face.y = 0f;
            _player.Controller.Teleport(step, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : _player.transform.forward));
        }

        private static Transform NearestCoverProp(Vector3 near)
        {
            Transform best = null;
            var bestSqr = float.MaxValue;
            foreach (var p in CoverProp.Registry)
            {
                var sqr = (p.transform.position - near).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = p.transform;
            }
            return best;
        }
    }
}

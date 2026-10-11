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
    /// Self-play verification. Launch the player with <c>-autoplay [-artifacts DIR]</c> and it tours the
    /// demo through the real systems — cameras, rummaging, doors, an ATB battle with the range dome,
    /// a grenade, a Focus window — writing a screenshot at each beat, then quits. "Done is verified by
    /// play": look at the PNGs.
    /// </summary>
    public sealed class Autopilot : MonoBehaviour
    {
        private string _dir;
        private int _shot;
        private PlayerCharacter _player;

        public static bool Requested => Environment.GetCommandLineArgs().Contains("-autoplay");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Requested) return;
            new GameObject("Autopilot").AddComponent<Autopilot>();
        }

        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
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
            _offscreen = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
            var panel = Instantiate(doc.panelSettings);
            panel.targetTexture = _offscreen;
            panel.clearColor = true;
            panel.colorClearValue = Color.black;
            doc.panelSettings = panel;
            Log("offscreen capture via panel render texture");
        }

        private IEnumerator Shot(string name)
        {
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
            var session = GameSession.Instance;
            if (session == null)
            {
                Log("FAIL no GameSession in scene");
                Application.Quit(2);
                yield break;
            }
            _player = session.Player;
            var failures = 0;

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
            foreach (var zone in FindObjectsByType<EncounterZone>(FindObjectsSortMode.None).OrderBy(z => z.name))
            {


                Teleport(zone.Bounds.center - new Vector3(0f, zone.Bounds.extents.y - 0.1f, 0f), zone.Bounds.center + Vector3.forward);
                yield return Real(0.3f);
                var battle = BattleDirector.Instance;
                if (!battle.InBattle)
                {
                    Log($"FAIL battle did not start in {zone.name}");
                    failures++;
                    continue;
                }
                yield return Real(1.4f);
                yield return Shot($"{zone.name.ToLowerInvariant()}_encounter");
                yield return FightToTheEnd(battle, zone.name.ToLowerInvariant());
                if (!zone.Cleared)
                {
                    Log($"FAIL {zone.name} not cleared");
                    failures++;
                }
                yield return Real(0.5f);
            }

            // 4. The shed is padlocked until the key from the Mercer side table is in the bag.
            var shedDoor = FindObjectsByType<Door>(FindObjectsSortMode.None).FirstOrDefault(d => d.name == "Door_Shed_Enter");
            if (shedDoor == null)
            {
                Log("FAIL no Door_Shed_Enter in scene");
                failures++;
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
                    failures++;
                }
            }

            // 5. Enter the house, search everything inside, including the side table with the shed key.
            var houseDoor = FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(d => d.name).FirstOrDefault(d => d.name.Contains("Enter"));
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
                    failures++;
                }
                Teleport(shedDoor.ApproachPoint, shedDoor.transform.position);
                yield return Real(0.4f);
                shedDoor.Interact(_player);
                yield return Real(1.9f);
                yield return Shot("shed_interior");
                if (shedDoor.Locked || !IsNear(shedDoor.Destination))
                {
                    Log("FAIL shed did not open with the key");
                    failures++;
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
                        failures++;
                    }
                }
            }

            // 7. Field menu.
            session.OpenFieldMenu();
            yield return Real(0.3f);
            yield return Shot("field_menu");
            session.CloseMenu();

            Log(failures == 0 ? "PASS autoplay complete" : $"FAIL {failures} step(s) failed");
            yield return Real(0.5f);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator FightToTheEnd(BattleDirector battle, string tag)
        {
            var turn = 0;
            var focusShown = false;
            var timeout = Time.realtimeSinceStartup + 90f;
            while (battle.InBattle && battle.Phase != BattlePhase.Victory && Time.realtimeSinceStartup < timeout)
            {
                // The autopilot is a tourist, not a test of skill: keep it alive.
                if (_player.Health.Fraction < 0.5f) _player.Heal(999);

                if (!focusShown && turn == 1 && battle.Phase == BattlePhase.Active && battle.DebugOpenFocus())
                {
                    focusShown = true;
                    yield return Real(0.5f);
                    yield return Shot($"{tag}_focus");
                    battle.UseFocusMove(FocusMove.TargetVitals);
                    yield return Real(0.3f);
                    yield return Shot($"{tag}_vitals");
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
            yield return Real(0.4f);
            if (battle.Phase == BattlePhase.Victory) yield return Shot($"{tag}_victory");
            while (battle.InBattle && Time.realtimeSinceStartup < timeout) yield return null;
        }
    }
}

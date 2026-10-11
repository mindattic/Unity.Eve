using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEve.Battle;
using UnityEve.Cameras;
using UnityEve.Core.Battle;
using UnityEve.Data;
using UnityEve.Input;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Game
{
    /// <summary>
    /// Composition root and game flow: the title screen (New Game / Continue / Quit), the field pause
    /// menu (items / equip / controls), HUD status each frame, defeat (continue from the last save or
    /// retry), and the ending. Scene references are wired by the content builder.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        private static readonly object MenuPauseKey = new object();
        private static readonly object TitlePauseKey = new object();

        [SerializeField] private PlayerCharacter _player;
        [SerializeField] private Transform _spawn;
        [SerializeField] private CameraShot _spawnShot;
        [SerializeField] private ItemDefinition[] _startingItems = new ItemDefinition[0];
        [SerializeField] private int[] _startingCounts = new int[0];
        [SerializeField] private bool _showTitle = true;

        private MenuModel _menu;
        private readonly System.Collections.Generic.Stack<MenuModel> _stack = new System.Collections.Generic.Stack<MenuModel>();
        private bool _onTitle;

        public static GameSession Instance { get; private set; }
        public PlayerCharacter Player => _player;
        public bool OnTitle => _onTitle;
        public bool MenuOpen => _menu != null;

        public void Configure(PlayerCharacter player, Transform spawn, CameraShot spawnShot, ItemDefinition[] items, int[] counts)
        {
            _player = player;
            _spawn = spawn;
            _spawnShot = spawnShot;
            _startingItems = items;
            _startingCounts = counts;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            if (GameHud.Instance != null && CameraDirector.Instance != null) GameHud.Instance.WorldCamera = CameraDirector.Instance.Camera;

            // Continue: a save is waiting from the scene reload. Put the night back and play on.
            if (SaveSystem.Pending != null)
            {
                yield return null; // let every world object finish Awake/Start first
                SaveSystem.ApplyPending();
                GameHud.Instance?.ShowMessage("Progress loaded.", 2.5f);
                yield break;
            }

            for (var i = 0; i < _startingItems.Length; i++)
                if (_startingItems[i] != null) _player.Bag.Add(_startingItems[i].Id, i < _startingCounts.Length ? _startingCounts[i] : 1);
            if (!_showTitle || Autopilot.Requested) yield break;
            ShowTitle();
        }

        /// <summary>The title screen (also used by autoplay to check it renders).</summary>
        public void ShowTitle()
        {
            _onTitle = true;
            WorldTime.Instance?.Pause(TitlePauseKey);
            _player.Controller.Lock();
            GameHud.Instance?.ShowBanner("UNITY EVE", "Kingsport, June 21 1998. The shortest night of the year.", 0f);
            var save = SaveSystem.Read();
            var menu = new MenuModel("");
            menu.Add(new MenuEntry("New Game", StartGame, true, "Begin at the bus stop on Ashgrove Street."));
            menu.Add(new MenuEntry("Continue", () => { CloseTitleMenu(); SaveSystem.Load(); }, save != null,
                save != null ? $"Last save: {save.Summary}" : "No saved game yet. Payphones save your progress."));
            menu.Add(new MenuEntry("Quit", Application.Quit, true, "Leave Kingsport."));
            _menu = menu;
            _stack.Clear();
            GameHud.Instance?.ShowMenu(menu);
        }

        private void CloseTitleMenu()
        {
            _menu = null;
            GameHud.Instance?.ShowMenu(null);
        }

        public void StartGame()
        {
            if (!_onTitle) return;
            _onTitle = false;
            CloseTitleMenu();
            WorldTime.Instance?.Resume(TitlePauseKey);
            _player.Controller.Unlock();
            GameHud.Instance?.HideBanner();
            AudioService.Play(SoundId.UiConfirm);
            GameHud.Instance?.ShowMessage("Something is wrong with the town tonight. Find out what happened to the Mercers.", 4.5f);
            var story = StoryDirector.Instance;
            if (story != null && story.CurrentText != null) GameHud.Instance?.ShowBanner("OBJECTIVE", story.CurrentText, 3f);
        }

        private void Update()
        {
            var input = GameInput.Instance;
            if (_onTitle || _endScreen)
            {
                if (_menu != null && input != null)
                {
                    if (input.NavigateStep.y != 0) _menu.Move(-input.NavigateStep.y);
                    if (input.ConfirmPressed) _menu.Confirm();
                }
                return;
            }

            PushStatus();

            var battle = BattleDirector.Instance;
            var inBattle = battle != null && battle.InBattle;
            if (_menu != null)
            {
                HandleMenuInput(input);
            }
            else if (input != null && input.MenuPressed && !inBattle && !_player.IsDead && Time.timeScale > 0f)
            {
                OpenFieldMenu();
            }
        }

        private void PushStatus()
        {
            var hud = GameHud.Instance;
            if (hud == null) return;
            var battle = BattleDirector.Instance;
            var w = _player.Weapon;
            var ammo = w == null ? "" : w.Stats.Melee ? w.Stats.DisplayName
                : $"{w.Stats.DisplayName}  {_player.RoundsInClip}/{w.Stats.ClipSize}  [{(w.Ammo != null ? _player.Bag.CountOf(w.Ammo.Id) : 0)}]";
            if (_player.Hasted) ammo += "  HASTE";
            if (_player.Barrier) ammo += "  BARRIER";
            hud.SetStatus(_player.Progression.Level, _player.Health.Current, _player.Health.Max,
                _player.Pe.Current, _player.Pe.Max,
                battle != null ? battle.Atb.Value : 0f, battle != null && battle.Atb.IsFull, ammo);
        }

        // ------------------------------------------------------------------ field menu

        public void OpenFieldMenu()
        {
            WorldTime.Instance?.Pause(MenuPauseKey);
            _player.Controller.Lock();
            _stack.Clear();
            Show(BuildFieldMenu());
            AudioService.Play(SoundId.UiConfirm);
        }

        private MenuModel BuildFieldMenu()
        {
            var p = _player.Progression;
            var menu = new MenuModel($"KAT   LV {p.Level}   EXP {p.Exp}/{p.ExpToNext}   AT {p.ActiveTime}");
            menu.Add(new MenuEntry("Items", () => Push(BuildItemsMenu()), true, $"{_player.Bag.Slots.Count}/{_player.Bag.Capacity} slots used."));
            menu.Add(new MenuEntry("Equip", () => Push(BuildEquipMenu()), _player.Weapons.Length > 1, "Choose a weapon."));
            menu.Add(new MenuEntry("Controls", () => Push(BuildControlsMenu()), true, "How to play."));
            menu.Add(new MenuEntry("Resume", CloseMenu));
            return menu;
        }

        private MenuModel BuildItemsMenu()
        {
            var menu = new MenuModel("ITEMS");
            foreach (var group in _player.Bag.Slots.GroupBy(s => s.ItemId))
            {
                var def = _player.Items.Find(group.Key);
                if (def == null) continue;
                var usable = def.Stats.Effect == ItemEffect.RestoreHp && _player.Health.Current < _player.Health.Max;
                menu.Add(new MenuEntry($"{def.Stats.DisplayName,-14} x{_player.Bag.CountOf(def.Id)}", () =>
                {
                    if (!_player.Bag.TryRemove(def.Id)) return;
                    var healed = _player.Heal(def.Stats.Power);
                    AudioService.Play(SoundId.Heal);
                    GameHud.Instance?.ShowMessage($"Recovered {healed} HP.");
                    Replace(BuildItemsMenu());
                }, usable, def.Stats.Description));
            }
            if (menu.Entries.Count == 0) menu.Add(new MenuEntry("(empty)", null, false));
            return menu;
        }

        private MenuModel BuildEquipMenu()
        {
            var menu = new MenuModel("EQUIP");
            foreach (var weapon in _player.Weapons)
            {
                var w = weapon;
                menu.Add(new MenuEntry((w == _player.Weapon ? "*" : " ") + w.Stats.DisplayName, () =>
                {
                    _player.Equip(w);
                    Back();
                }, true, $"Dmg {w.Stats.Damage}  Range {w.Stats.Range:0}m  AT x{w.Stats.AtbMultiplier:0.0}" + (w.Stats.Burst ? "  Burst" : "")));
            }
            return menu;
        }

        private MenuModel BuildControlsMenu()
        {
            var menu = new MenuModel("CONTROLS  (gamepad / keyboard)");
            menu.Add(new MenuEntry("Move: left stick / WASD", null, true,
                "Explore in real time. Fixed and CCTV cameras cut as you move; 'up' keeps its meaning across cuts until you let go."));
            menu.Add(new MenuEntry("Interact / search: X / E", null, true, "Hold to rummage through drawers, fridges and crates."));
            menu.Add(new MenuEntry("Battle command: A / Enter", null, true,
                "When the AT gauge is full, time stops. Attack raises the range dome: only enemies inside can be hit."));
            menu.Add(new MenuEntry("Dodge / back: B / Space", null, true,
                "Dodge just as a glowing enemy strikes for FOCUS: slow motion and a free Quick Heal, Target Vitals or Counter. Optional, never required."));
            menu.Add(new MenuEntry("Menu: Start / Esc", null, true, "Items, equipment, this screen."));
            return menu;
        }

        private void Show(MenuModel menu)
        {
            if (_menu != null) _stack.Push(_menu);
            _menu = menu;
            GameHud.Instance?.ShowMenu(menu);
        }

        private void Push(MenuModel menu) => Show(menu);

        private void Back()
        {
            AudioService.Play(SoundId.UiCancel);
            if (_stack.Count == 0)
            {
                CloseMenu();
                return;
            }
            var previous = _stack.Pop();
            // Rebuild the root so its header (level, EXP) is current.
            Replace(_stack.Count == 0 ? BuildFieldMenu() : previous);
        }

        private void Replace(MenuModel menu)
        {
            _menu = menu;
            GameHud.Instance?.ShowMenu(menu);
        }

        public void CloseMenu()
        {
            _menu = null;
            _stack.Clear();
            GameHud.Instance?.ShowMenu(null);
            WorldTime.Instance?.Resume(MenuPauseKey);
            _player.Controller.Unlock();
            _player.Controller.SuppressDodgeThisFrame();
        }

        private void HandleMenuInput(GameInput input)
        {
            if (input == null) return;
            if (input.NavigateStep.y != 0) _menu.Move(-input.NavigateStep.y);
            if (input.ConfirmPressed) _menu.Confirm();
            else if (input.CancelPressed || input.MenuPressed) Back();
        }

        // ------------------------------------------------------------------ defeat

        private bool _endScreen;
        public bool OnEndScreen => _endScreen;

        public void OnPlayerDefeated(EncounterZone zone)
        {
            StartCoroutine(DefeatRoutine());
        }

        private IEnumerator DefeatRoutine()
        {
            var hud = GameHud.Instance;
            hud?.ShowBanner("YOU DIED", "Kingsport keeps her... for now.", 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            if (Autopilot.Requested)
            {
                yield return RetryAtBusStop();
                yield break;
            }
            // Continue from the last save, or wake at the bus stop.
            _endScreen = true;
            var save = SaveSystem.Read();
            var menu = new MenuModel("");
            menu.Add(new MenuEntry("Continue", () => { _endScreen = false; CloseTitleMenu(); SaveSystem.Load(); }, save != null,
                save != null ? $"From {save.Location}. {save.Summary}" : "No saved game yet."));
            menu.Add(new MenuEntry("Retry at the bus stop", () => { _endScreen = false; CloseTitleMenu(); StartCoroutine(RetryAtBusStop()); }, true,
                "Keep everything you carry; wake where the night began."));
            _menu = menu;
            hud?.ShowMenu(menu);
        }

        private IEnumerator RetryAtBusStop()
        {
            var hud = GameHud.Instance;
            hud?.HideBanner();
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
            {
                hud?.SetFade(t);
                yield return null;
            }
            _player.Revive();
            _player.Controller.Animator?.SetTrigger(AnimParams.Revive);
            _player.Controller.Teleport(_spawn.position, _spawn.rotation);
            CameraDirector.Instance?.ResetZones(_spawnShot);
            yield return new WaitForFixedUpdate();
            for (var t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.5f)
            {
                hud?.SetFade(t);
                yield return null;
            }
            hud?.SetFade(0f);
            hud?.ShowMessage("You wake at the bus stop. The creatures are waiting.");
        }
    
        // ------------------------------------------------------------------ ending

        /// <summary>The last objective fell: the ending card and the night's tally, then the title.</summary>
        public void Ending() => StartCoroutine(EndingRoutine());

        private IEnumerator EndingRoutine()
        {
            var hud = GameHud.Instance;
            yield return new WaitForSecondsRealtime(2.5f);
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
            {
                hud?.SetFade(t * 0.85f);
                yield return null;
            }
            _endScreen = true;
            _player.Controller.Lock();
            var story = StoryDirector.Instance;
            var time = System.TimeSpan.FromSeconds(story != null ? story.PlaySeconds : 0f);
            var save = SaveSystem.Read();
            hud?.ShowBanner("THE NIGHT IS NOT OVER",
                "The Mother is ash. Under Kingsport, something else is still singing.\n\n" +
                $"Time {(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}    Creatures {(story != null ? story.Kills : 0)}    " +
                $"Saves {(save != null ? save.Saves : 0)}    Level {_player.Progression.Level}", 0f);
            var menu = new MenuModel("");
            menu.Add(new MenuEntry("Back to the title", () => { _endScreen = false; CloseTitleMenu(); WorldTime.Instance?.ClearAll();
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); }, true, "Thank you for playing the Unity Eve vertical slice."));
            _menu = menu;
            hud?.ShowMenu(menu);
        }
    }
}

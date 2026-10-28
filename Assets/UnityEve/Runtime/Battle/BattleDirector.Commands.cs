using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Session;
using Unity.Eve.Core.Sim;
using Unity.Eve.Data;
using Unity.Eve.Enemies;
using Unity.Eve.Game;
using Unity.Eve.UI;

namespace Unity.Eve.Battle
{
    /// <summary>
    /// The paused half of the battle as the seat sees it: command menus, the range dome and Line Rush's aim
    /// line. All of it is presentation: each finished choice goes to the session as one <see cref="BattleCommand"/>,
    /// and the authority decides whether it happens.
    /// </summary>
    public sealed partial class BattleDirector
    {
        private readonly List<EnemyActor> _targets = new List<EnemyActor>();
        private readonly Stack<MenuModel> _menuStack = new Stack<MenuModel>();
        private MenuModel _menu;
        private BattlePhase _menuPhase = BattlePhase.Command;
        private int _targetIndex;
        private System.Func<EnemyActor, bool> _onTargetChosen;
        private float _aimRange;
        private Vector3 _aimDirection;
        private System.Func<Vector3, bool> _onAimChosen;
        private LineRenderer _aimLine;

        // ------------------------------------------------------------------ menus

        /// <summary>Stops time for the command menu when the gauge is full (the self-play's way in).</summary>
        public void OpenCommandMenu()
        {
            if (Phase != BattlePhase.Active || !Rules.Atb.IsFull || Rules.Focus.IsOpen) return;
            Submit(BattleOrder.OpenMenu);
        }

        private MenuModel BuildCommandMenu()
        {
            var w = _player.Weapon;
            var menu = new MenuModel("COMMAND");
            if (_player.NeedsReload)
            {
                menu.Add(new MenuEntry("Reload", () => Submit(BattleOrder.Reload), _player.CanReload,
                    _player.CanReload ? "Load a fresh magazine. Takes the turn." : "No ammunition in the bag."));
            }
            else
            {
                var ammo = w != null && !w.Stats.Melee ? $" ({_player.RoundsInClip}/{w.Stats.ClipSize})" : "";
                menu.Add(new MenuEntry("Attack" + ammo, BeginAttackTargeting, w != null,
                    w != null ? $"{w.Stats.DisplayName}: {w.Stats.Damage} dmg, range {w.Stats.Range:0}m" + (w.Stats.Burst ? ", hits all in range" : "") : null));
                if (w != null && w.Stats.BurstFireRounds > 0)
                {
                    menu.Add(new MenuEntry("Burst Fire", BeginBurstFireTargeting, _player.RoundsInClip > 0,
                        $"Spray {w.Stats.BurstFireRounds} rounds at one target, {Mathf.RoundToInt(w.Stats.BurstFireAccuracy * 100)}% per round. Burns ammo fast."));
                }
            }
            menu.Add(new MenuEntry("Blade", () => PushMenu(BuildBladeMenu()), _player.Katana != null, "Katana arts: Slash and Rush."));
            menu.Add(new MenuEntry("PE", () => PushMenu(BuildPeMenu()), _player.Abilities.Length > 0, "Parasite Energy abilities."));
            menu.Add(new MenuEntry("Item", () => PushMenu(BuildItemMenu()), true, "Medicine and throwables."));
            menu.Add(new MenuEntry("Equip", () => PushMenu(BuildEquipMenu()), _player.Weapons.Length > 1, "Swap weapons. Takes no turn."));
            var strongest = _enemies.Where(e => e.IsAlive).Select(e => e.Stats.Level).DefaultIfEmpty(1).Max();
            var chance = EscapeRule.Chance(_player.Progression.Level, strongest);
            menu.Add(new MenuEntry("Escape", () => Submit(BattleOrder.Escape), true, $"Run for it. {Mathf.RoundToInt(chance * 100)}% chance."));
            return menu;
        }

        private MenuModel BuildBladeMenu()
        {
            var menu = new MenuModel("BLADE");
            var power = BladeArts.Power(BattleConfig.KatanaDamage, _player.Progression.Level, rush: false);
            menu.Add(new MenuEntry("Slash", () => BeginTargeting(BattleConfig.KatanaSlashReach, target => Submit(BattleOrder.Slash, target.State.Id)), true,
                $"A heavy cut: {power} dmg to the target and anything beside it. Reach {BattleConfig.KatanaSlashReach:0.0}m."));
            var cost = BattleConfig.KatanaRushPeCost;
            var affordable = _player.Pe.CanAfford(cost);
            menu.Add(new MenuEntry($"Rush  {cost:0} PE", () => BeginTargeting(BattleConfig.KatanaRushRange, target => Submit(BattleOrder.Rush, target.State.Id)), affordable,
                $"Dash through a target up to {BattleConfig.KatanaRushRange:0}m away, cutting everything in the way. Untouchable mid-dash."));
            menu.Add(new MenuEntry($"Line Rush  {cost:0} PE", () => BeginAiming(BattleConfig.KatanaRushRange, end => Submit(BattleOrder.LineRush, x: end.x, z: end.z)), affordable,
                $"Aim the dash yourself: rotate the line with the stick, confirm to cut everything it crosses, up to {BattleConfig.KatanaRushRange:0}m. Same cut, your aim."));
            return menu;
        }

        private MenuModel BuildPeMenu()
        {
            var menu = new MenuModel($"PE  {Mathf.FloorToInt(_player.Pe.Current)}/{Mathf.FloorToInt(_player.Pe.Max)}");
            var abilities = _player.Abilities;
            for (var i = 0; i < abilities.Length; i++)
            {
                var a = abilities[i];
                var index = i;
                var affordable = _player.Pe.CanAfford(a.Stats.PeCost);
                menu.Add(new MenuEntry($"{a.Stats.DisplayName,-12} {a.Stats.PeCost,3:0}", () => ChooseAbility(a, index), affordable, a.Stats.Description));
            }
            return menu;
        }

        private MenuModel BuildItemMenu()
        {
            var menu = new MenuModel("ITEM");
            foreach (var slot in _player.Bag.Slots.GroupBy(s => s.ItemId))
            {
                var def = _player.Items.Find(slot.Key);
                if (def == null || !def.Stats.UsableInBattle) continue;
                var count = _player.Bag.CountOf(slot.Key);
                menu.Add(new MenuEntry($"{def.Stats.DisplayName,-14} x{count}", () => ChooseItem(def), true, def.Stats.Description));
            }
            if (menu.Entries.Count == 0) menu.Add(new MenuEntry("(nothing usable)", null, false));
            return menu;
        }

        private MenuModel BuildEquipMenu()
        {
            var menu = new MenuModel("EQUIP");
            foreach (var weapon in _player.Weapons)
            {
                var w = weapon;
                var mark = w == _player.Weapon ? "*" : " ";
                menu.Add(new MenuEntry($"{mark}{w.Stats.DisplayName}", () =>
                {
                    if (!Submit(BattleOrder.Equip, index: _player.IndexOf(w))) return;
                    GameHud.Instance?.ShowMessage($"{w.Stats.DisplayName} ready.");
                    _menuStack.Clear();
                    PushMenu(BuildCommandMenu());
                }, true, $"Dmg {w.Stats.Damage}  Range {w.Stats.Range:0}m  AT x{w.Stats.AtbMultiplier:0.0}"
                    + (w.Stats.Burst ? "  Burst" : "") + (w.Stats.BurstFireRounds > 0 ? "  Burst Fire" : "")));
            }
            return menu;
        }

        private void PushMenu(MenuModel menu)
        {
            if (_menu != null) _menuStack.Push(_menu);
            _menu = menu;
            GameHud.Instance?.ShowMenu(menu);
        }

        private void PopMenu()
        {
            AudioService.Play(SoundId.UiCancel);
            if (_menuStack.Count > 0)
            {
                _menu = _menuStack.Pop();
                // Rebuild the root so labels (ammo, PE) are current.
                if (_menuStack.Count == 0) _menu = BuildCommandMenu();
                GameHud.Instance?.ShowMenu(_menu);
                return;
            }
            // Cancel on the root menu returns to real time with the gauge still full.
            Submit(BattleOrder.CancelMenu);
        }

        /// <summary>Takes down everything the paused menu raised; the authority has already let time run.</summary>
        private void CloseMenus()
        {
            _menu = null;
            _menuStack.Clear();
            _menuPhase = BattlePhase.Command;
            _dome.Hide();
            GameHud.Instance?.ShowMenu(null);
            // Aiming can be left behind by paths that don't go through EndAiming (the player dying or the battle
            // ending outright while the aim line is up): never leak the line or leave its prompt on screen.
            if (_aimLine != null)
            {
                Destroy(_aimLine.gameObject);
                _aimLine = null;
            }
            GameHud.Instance?.ShowPrompt(null);
            WorldTime.Instance?.Resume(PauseKey);
        }

        private void HandleMenuInput(PlayerControls input)
        {
            if (input == null || _menu == null) return;
            if (input.NavigateY != 0) _menu.Move(-input.NavigateY);
            if (input.ConfirmPressed) _menu.Confirm();
            else if (input.CancelPressed) PopMenu();
        }

        /// <summary>Back to the menu after a choice the authority turned down.</summary>
        private void BackToMenu()
        {
            AudioService.Play(SoundId.UiDeny);
            _dome.Hide();
            _menuPhase = BattlePhase.Command;
            GameHud.Instance?.ShowMenu(_menu);
        }

        // ------------------------------------------------------------------ targeting (range dome)

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private void BeginTargeting(float range, System.Func<EnemyActor, bool> onChosen)
        {
            // Measured on the authority's positions, the same test it applies to the order.
            var origin = _player.transform.position;
            _targets.Clear();
            _targets.AddRange(_enemies
                .Where(e => e.IsAlive && Flat(e.State.Position.ToUnity() - origin) <= range + e.State.BodyRadius)
                .OrderBy(e => Flat(e.State.Position.ToUnity() - origin)));
            if (_targets.Count == 0)
            {
                AudioService.Play(SoundId.UiDeny);
                GameHud.Instance?.ShowMessage("Nothing in range.");
                _dome.Show(origin, range);
                StartCoroutine(HideDomeSoon());
                return;
            }
            _onTargetChosen = onChosen;
            _targetIndex = 0;
            _menuPhase = BattlePhase.Targeting;
            GameHud.Instance?.ShowMenu(null);
            _dome.Show(origin, range);
            RefreshTargetPanel();
        }

        private IEnumerator HideDomeSoon()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            if (Phase == BattlePhase.Command) _dome.Hide();
        }

        private void RefreshTargetPanel()
        {
            _targets.RemoveAll(t => !t.IsAlive);
            if (_targets.Count == 0) return;
            _targetIndex = Mathf.Clamp(_targetIndex, 0, _targets.Count - 1);
            var t = _targets[_targetIndex];
            _dome.Mark(t.AimPoint);
            GameHud.Instance?.ShowTarget($"{t.Stats.DisplayName}  LV{t.Stats.Level}  HP {t.Health.Current}/{t.Health.Max}", t.Health.Fraction);
        }

        private void HandleTargetInput(PlayerControls input)
        {
            if (input == null) return;
            var step = input.NavigateX != 0 ? input.NavigateX : -input.NavigateY;
            if (step != 0 && _targets.Count > 0)
            {
                _targetIndex = (_targetIndex + step + _targets.Count) % _targets.Count;
                AudioService.Play(SoundId.UiMove, 0.6f);
                RefreshTargetPanel();
            }
            if (input.ConfirmPressed && _targets.Count > 0)
            {
                AudioService.Play(SoundId.UiConfirm);
                ChooseTarget(_targets[_targetIndex]);
            }
            else if (input.CancelPressed)
            {
                AudioService.Play(SoundId.UiCancel);
                _dome.Hide();
                _menuPhase = BattlePhase.Command;
                GameHud.Instance?.ShowMenu(_menu);
            }
        }

        private void ChooseTarget(EnemyActor target)
        {
            _dome.Hide();
            if (_onTargetChosen == null || !_onTargetChosen(target)) BackToMenu();
        }

        // ------------------------------------------------------------------ aiming (Line Rush)

        private void BeginAiming(float range, System.Func<Vector3, bool> onChosen)
        {
            _aimRange = range;
            _onAimChosen = onChosen;
            _aimDirection = _player.transform.forward;
            _menuPhase = BattlePhase.Aiming;
            GameHud.Instance?.ShowMenu(null);
            GameHud.Instance?.ShowPrompt("Rotate to aim — [A] dash  [B] cancel");
            _dome.Show(_player.transform.position, range);
            EnsureAimLine();
            RefreshAimLine();
        }

        private void EnsureAimLine()
        {
            if (_aimLine != null) return;
            var go = new GameObject("RushAimLine");
            _aimLine = go.AddComponent<LineRenderer>();
            _aimLine.sharedMaterial = BattleFx.Instance != null ? BattleFx.Instance.Additive : null;
            _aimLine.positionCount = 2;
            _aimLine.startWidth = BattleConfig.KatanaRushHalfWidth * 2f;
            _aimLine.endWidth = BattleConfig.KatanaRushHalfWidth * 2f;
            _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Redraws the aim line from Kat out to the rush range, brightening it while it crosses at least
        /// one creature so lining up a multi-cut reads clearly before committing. She turns to show the aim (a
        /// look only: her facing in the rules changes when the dash itself starts).</summary>
        private void RefreshAimLine()
        {
            var start = _player.transform.position;
            var (ex, ez) = BladeArts.AimedRushEnd(start.x, start.z, _aimDirection.x, _aimDirection.z, _aimRange);
            var end = new Vector3(ex, start.y, ez);
            _aimLine.SetPosition(0, start + Vector3.up * 1.0f);
            _aimLine.SetPosition(1, end + Vector3.up * 1.0f);
            var crosses = _enemies.Any(e => e.IsAlive && BladeArts.OnRushPath(start.x, start.z, ex, ez, e.State.Position.X, e.State.Position.Z, e.State.BodyRadius));
            var color = crosses ? new Color(1f, 0.85f, 0.3f) : BladeColor;
            _aimLine.startColor = color;
            _aimLine.endColor = color;
            var look = end - start;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) _player.transform.rotation = Quaternion.LookRotation(look);
        }

        private void HandleAimInput(PlayerControls input)
        {
            if (input == null) return;
            var turn = input.MoveX;
            if (Mathf.Abs(turn) > 0.05f)
                _aimDirection = Quaternion.Euler(0f, turn * BattleConfig.RushAimTurnDegreesPerSecond * Time.unscaledDeltaTime, 0f) * _aimDirection;
            RefreshAimLine();
            if (input.ConfirmPressed) ConfirmAim();
            else if (input.CancelPressed)
            {
                AudioService.Play(SoundId.UiCancel);
                EndAiming();
                _menuPhase = BattlePhase.Command;
                GameHud.Instance?.ShowMenu(_menu);
            }
        }

        private void ConfirmAim()
        {
            var start = _player.transform.position;
            var (ex, ez) = BladeArts.AimedRushEnd(start.x, start.z, _aimDirection.x, _aimDirection.z, _aimRange);
            AudioService.Play(SoundId.UiConfirm);
            var onChosen = _onAimChosen;
            EndAiming();
            if (onChosen == null || !onChosen(new Vector3(ex, start.y, ez))) BackToMenu();
        }

        private void EndAiming()
        {
            _dome.Hide();
            if (_aimLine != null) Destroy(_aimLine.gameObject);
            _aimLine = null;
            GameHud.Instance?.ShowPrompt(null);
        }

        /// <summary>Scripted use for the autopilot: aims at the nearest living enemy and confirms at once.</summary>
        public bool AutoConfirmAim()
        {
            if (Phase != BattlePhase.Aiming) return false;
            var origin = _player.transform.position;
            var nearest = _enemies.Where(e => e.IsAlive).OrderBy(e => Flat(e.State.Position.ToUnity() - origin)).FirstOrDefault();
            if (nearest != null)
            {
                var d = nearest.State.Position.ToUnity() - origin;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) _aimDirection = d.normalized;
            }
            ConfirmAim();
            return true;
        }

        // ------------------------------------------------------------------ choices

        private void BeginAttackTargeting()
        {
            var w = _player.Weapon;
            BeginTargeting(w.Stats.Range, target => Submit(BattleOrder.Attack, target.State.Id));
        }

        private void BeginBurstFireTargeting()
        {
            var w = _player.Weapon;
            BeginTargeting(w.Stats.Range, target => Submit(BattleOrder.BurstFire, target.State.Id));
        }

        private void ChooseAbility(AbilityDefinition ability, int index)
        {
            if (ability.Stats.NeedsTarget)
                BeginTargeting(ability.Stats.Range, target => Submit(BattleOrder.Ability, target.State.Id, index));
            else if (!Submit(BattleOrder.Ability, index: index))
                AudioService.Play(SoundId.UiDeny);
        }

        private void ChooseItem(ItemDefinition item)
        {
            switch (item.Stats.Effect)
            {
                case ItemEffect.Explosive:
                case ItemEffect.Flash:
                    BeginTargeting(_throwRange, target => Submit(BattleOrder.Item, target.State.Id, item: item.Id));
                    break;
                default:
                    if (!Submit(BattleOrder.Item, item: item.Id)) AudioService.Play(SoundId.UiDeny);
                    break;
            }
        }

        private static readonly Color BladeColor = new Color(0.55f, 0.95f, 1f);

        // ------------------------------------------------------------------ automation (autopilot, play-mode tests)

        /// <summary>Selects and confirms the first menu entry whose label starts with <paramref name="prefix"/>.</summary>
        public bool AutoChoose(string prefix)
        {
            if (Phase != BattlePhase.Command || _menu == null) return false;
            var index = _menu.Entries.FindIndex(e => e.Label.TrimStart().StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            _menu.Select(index);
            return _menu.Confirm();
        }

        /// <summary>Backs out of any open menu to real time without spending the turn.</summary>
        public void AutoCancel()
        {
            if (Phase == BattlePhase.Command || Phase == BattlePhase.Targeting) Submit(BattleOrder.CancelMenu);
        }

        public bool AutoConfirmTarget()
        {
            if (Phase != BattlePhase.Targeting || _targets.Count == 0) return false;
            ChooseTarget(_targets[_targetIndex]);
            return true;
        }
    }
}

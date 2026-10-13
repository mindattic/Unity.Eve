using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEve.Cameras;
using UnityEve.Core.Battle;
using UnityEve.Data;
using UnityEve.Enemies;
using UnityEve.Game;
using UnityEve.Input;
using UnityEve.UI;

namespace UnityEve.Battle
{
    /// <summary>The paused half of the battle: command menus, the range dome, and executing turns.</summary>
    public sealed partial class BattleDirector
    {
        // ------------------------------------------------------------------ menus

        public void OpenCommandMenu()
        {
            if (Phase != BattlePhase.Active || !_atb.IsFull || _focus.IsOpen) return;
            Phase = BattlePhase.Command;
            WorldTime.Instance?.Pause(PauseKey);
            _player.Controller.Lock();
            _menuLocked = true;
            _menuStack.Clear();
            PushMenu(BuildCommandMenu());
        }

        private MenuModel BuildCommandMenu()
        {
            var w = _player.Weapon;
            var menu = new MenuModel("COMMAND");
            if (_player.NeedsReload)
            {
                menu.Add(new MenuEntry("Reload", DoReload, _player.CanReload,
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
            menu.Add(new MenuEntry("Escape", DoEscape, true, $"Run for it. {Mathf.RoundToInt(chance * 100)}% chance."));
            return menu;
        }

        private MenuModel BuildBladeMenu()
        {
            var menu = new MenuModel("BLADE");
            var power = BladeArts.Power(BattleConfig.KatanaDamage, _player.Progression.Level, rush: false);
            menu.Add(new MenuEntry("Slash", () => BeginTargeting(BattleConfig.KatanaSlashReach, target => Execute(SlashRoutine(target), 0.75f)), true,
                $"A heavy cut: {power} dmg to the target and anything beside it. Reach {BattleConfig.KatanaSlashReach:0.0}m."));
            var cost = BattleConfig.KatanaRushPeCost;
            var affordable = _player.Pe.CanAfford(cost);
            menu.Add(new MenuEntry($"Rush  {cost:0} PE", () => BeginTargeting(BattleConfig.KatanaRushRange, target => Execute(RushRoutine(target), 0.9f)), affordable,
                $"Dash through a target up to {BattleConfig.KatanaRushRange:0}m away, cutting everything in the way. Untouchable mid-dash."));
            menu.Add(new MenuEntry($"Line Rush  {cost:0} PE", () => BeginAiming(BattleConfig.KatanaRushRange, end => Execute(RushLineRoutine(end), 0.9f)), affordable,
                $"Aim the dash yourself: rotate the line with the stick, confirm to cut everything it crosses, up to {BattleConfig.KatanaRushRange:0}m. Same cut, your aim."));
            return menu;
        }

        private MenuModel BuildPeMenu()
        {
            var menu = new MenuModel($"PE  {Mathf.FloorToInt(_player.Pe.Current)}/{Mathf.FloorToInt(_player.Pe.Max)}");
            foreach (var ability in _player.Abilities)
            {
                var a = ability;
                var affordable = _player.Pe.CanAfford(a.Stats.PeCost);
                menu.Add(new MenuEntry($"{a.Stats.DisplayName,-12} {a.Stats.PeCost,3:0}", () => ChooseAbility(a), affordable, a.Stats.Description));
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
                    _player.Equip(w);
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
            ResumeRealtime(spendTurn: false);
        }

        private void CloseMenus()
        {
            _menu = null;
            _menuStack.Clear();
            _dome.Hide();
            GameHud.Instance?.ShowMenu(null);
            // Defensive: Aiming can be left behind by paths that don't go through EndAiming (the player
            // dying or the battle ending outright while the aim line is up), which would otherwise leak
            // its LineRenderer and leave "Rotate to aim..." stuck on screen through Victory/Defeat.
            if (_aimLine != null)
            {
                Destroy(_aimLine.gameObject);
                _aimLine = null;
            }
            GameHud.Instance?.ShowPrompt(null);
            WorldTime.Instance?.Resume(PauseKey);
            ReleaseMenuLock();
        }

        private void ReleaseMenuLock()
        {
            if (!_menuLocked) return;
            _menuLocked = false;
            _player.Controller.Unlock();
            // The button that closed the menu must not also fire a dodge this frame.
            _player.Controller.SuppressDodgeThisFrame();
        }

        private void ResumeRealtime(bool spendTurn)
        {
            CloseMenus();
            if (spendTurn) _atb.Spend();
            Phase = BattlePhase.Active;
        }

        private void HandleMenuInput(GameInput input)
        {
            if (input == null || _menu == null) return;
            if (input.NavigateStep.y != 0) _menu.Move(-input.NavigateStep.y);
            if (input.ConfirmPressed) _menu.Confirm();
            else if (input.CancelPressed) PopMenu();
        }

        // ------------------------------------------------------------------ targeting (range dome)

        private void BeginTargeting(float range, System.Action<EnemyActor> onChosen)
        {
            var origin = _player.transform.position;
            _targets.Clear();
            _targets.AddRange(_enemies
                .Where(e => e.IsAlive && Flat(e.transform.position - origin) <= range + e.BodyRadius)
                .OrderBy(e => Flat(e.transform.position - origin)));
            if (_targets.Count == 0)
            {
                AudioService.Play(SoundId.UiDeny);
                GameHud.Instance?.ShowMessage("Nothing in range.");
                _dome.Show(origin, range);
                StartCoroutine(HideDomeSoon());
                return;
            }
            _targetRange = range;
            _onTargetChosen = onChosen;
            _targetIndex = 0;
            Phase = BattlePhase.Targeting;
            GameHud.Instance?.ShowMenu(null);
            _dome.Show(origin, range);
            RefreshTargetPanel();
        }

        private IEnumerator HideDomeSoon()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            if (Phase == BattlePhase.Command) _dome.Hide();
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private void RefreshTargetPanel()
        {
            _targets.RemoveAll(t => !t.IsAlive);
            if (_targets.Count == 0) return;
            _targetIndex = Mathf.Clamp(_targetIndex, 0, _targets.Count - 1);
            var t = _targets[_targetIndex];
            _dome.Mark(t.AimPoint);
            GameHud.Instance?.ShowTarget($"{t.Stats.DisplayName}  LV{t.Stats.Level}  HP {t.Health.Current}/{t.Health.Max}", t.Health.Fraction);
        }

        private void HandleTargetInput(GameInput input)
        {
            if (input == null) return;
            var step = input.NavigateStep.x != 0 ? input.NavigateStep.x : -input.NavigateStep.y;
            if (step != 0 && _targets.Count > 0)
            {
                _targetIndex = (_targetIndex + step + _targets.Count) % _targets.Count;
                AudioService.Play(SoundId.UiMove, 0.6f);
                RefreshTargetPanel();
            }
            if (input.ConfirmPressed && _targets.Count > 0)
            {
                AudioService.Play(SoundId.UiConfirm);
                var target = _targets[_targetIndex];
                _dome.Hide();
                _onTargetChosen?.Invoke(target);
            }
            else if (input.CancelPressed)
            {
                AudioService.Play(SoundId.UiCancel);
                _dome.Hide();
                Phase = BattlePhase.Command;
                GameHud.Instance?.ShowMenu(_menu);
            }
        }

        // ------------------------------------------------------------------ aiming (Line Rush)

        private void BeginAiming(float range, System.Action<Vector3> onChosen)
        {
            _aimRange = range;
            _onAimChosen = onChosen;
            _aimDirection = _player.transform.forward;
            Phase = BattlePhase.Aiming;
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

        /// <summary>Redraws the aim line from Kat out to <see cref="_aimRange"/>, brightening it while it
        /// currently crosses at least one enemy so lining up a multi-cut reads clearly before committing.</summary>
        private void RefreshAimLine()
        {
            var start = _player.transform.position;
            var (ex, ez) = BladeArts.AimedRushEnd(start.x, start.z, _aimDirection.x, _aimDirection.z, _aimRange);
            var end = new Vector3(ex, start.y, ez);
            _aimLine.SetPosition(0, start + Vector3.up * 1.0f);
            _aimLine.SetPosition(1, end + Vector3.up * 1.0f);
            var crosses = _enemies.Any(e => e.IsAlive && BladeArts.OnRushPath(start.x, start.z, ex, ez, e.transform.position.x, e.transform.position.z, e.BodyRadius));
            var color = crosses ? new Color(1f, 0.85f, 0.3f) : BladeColor;
            _aimLine.startColor = color;
            _aimLine.endColor = color;
            _player.Controller.FaceTowards(end);
        }

        private void HandleAimInput(GameInput input)
        {
            if (input == null) return;
            var turn = input.Move.x;
            if (Mathf.Abs(turn) > 0.05f)
                _aimDirection = Quaternion.Euler(0f, turn * BattleConfig.RushAimTurnDegreesPerSecond * Time.unscaledDeltaTime, 0f) * _aimDirection;
            RefreshAimLine();
            if (input.ConfirmPressed) ConfirmAim();
            else if (input.CancelPressed)
            {
                AudioService.Play(SoundId.UiCancel);
                EndAiming();
                Phase = BattlePhase.Command;
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
            onChosen?.Invoke(new Vector3(ex, start.y, ez));
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
            var nearest = _enemies.Where(e => e.IsAlive).OrderBy(e => Flat(e.transform.position - origin)).FirstOrDefault();
            if (nearest != null)
            {
                var d = nearest.transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) _aimDirection = d.normalized;
            }
            ConfirmAim();
            return true;
        }

        // ------------------------------------------------------------------ actions

        private void BeginAttackTargeting()
        {
            var w = _player.Weapon;
            BeginTargeting(w.Stats.Range, target => Execute(AttackRoutine(target), w.Stats.RootSeconds + 0.1f));
        }

        private void BeginBurstFireTargeting()
        {
            var w = _player.Weapon;
            BeginTargeting(w.Stats.Range, target => Execute(BurstFireRoutine(target), w.Stats.RootSeconds + 0.3f));
        }

        private void ChooseAbility(AbilityDefinition ability)
        {
            if (ability.Stats.NeedsTarget)
                BeginTargeting(ability.Stats.Range, target => Execute(AbilityRoutine(ability, target), 0.8f));
            else
                Execute(AbilityRoutine(ability, null), 0.8f);
        }

        private void ChooseItem(ItemDefinition item)
        {
            switch (item.Stats.Effect)
            {
                case ItemEffect.Explosive:
                case ItemEffect.Flash:
                    BeginTargeting(_throwRange, target => Execute(ThrowRoutine(item, target), 0.9f));
                    break;
                default:
                    Execute(UseItemRoutine(item), 0.6f);
                    break;
            }
        }

        private void DoReload() => Execute(ReloadRoutine(), BattleConfig.ActiveReloadDurationSeconds);

        private void DoEscape()
        {
            var strongest = _enemies.Where(e => e.IsAlive).Select(e => e.Stats.Level).DefaultIfEmpty(1).Max();
            if (_rng.Chance(EscapeRule.Chance(_player.Progression.Level, strongest)))
            {
                ResumeRealtime(spendTurn: true);
                GameHud.Instance?.ShowMessage("Kat slips away.");
                AudioService.Play(SoundId.Dodge);
                End(won: false);
            }
            else
            {
                GameHud.Instance?.ShowMessage("Couldn't escape!");
                AudioService.Play(SoundId.UiDeny);
                ResumeRealtime(spendTurn: true);
            }
        }

        /// <summary>Leaves the pause, roots the player and plays the turn out in real time.</summary>
        private void Execute(IEnumerator routine, float rootSeconds)
        {
            CloseMenus();
            _player.Controller.Root(rootSeconds);
            _atb.Spend();
            Phase = BattlePhase.Executing;
            StartCoroutine(ExecuteRoutine(routine));
        }

        private IEnumerator ExecuteRoutine(IEnumerator routine)
        {
            yield return routine;
            if (Phase == BattlePhase.Executing) Phase = BattlePhase.Active;
        }

        private IEnumerator AttackRoutine(EnemyActor chosen)
        {
            _lastAttackAt = Time.time;
            _lastAttackTarget = chosen;
            var w = _player.Weapon;
            var origin = _player.transform.position;
            var inRange = _enemies
                .Where(e => e.IsAlive && Flat(e.transform.position - origin) <= w.Stats.Range + e.BodyRadius)
                .OrderBy(e => e == chosen ? 0 : 1).ToList();
            var plan = DamageCalculator.PlanRounds(w.Stats, 0, inRange.Count, w.Stats.Melee ? int.MaxValue : _player.RoundsInClip);
            var share = DamageCalculator.RoundShare(w.Stats.RoundsPerTurn);
            _player.Controller.FaceTowards(chosen.transform.position);

            foreach (var index in plan)
            {
                var target = inRange[index];
                if (!target.IsAlive) continue;
                _player.Controller.FaceTowards(target.transform.position);
                _player.Controller.Animator?.SetTrigger(AnimParams.Shoot);
                if (!w.Stats.Melee) ShotVisual(target);
                var hit = DamageCalculator.Roll(WeaponDamage(), share, target.Stats.Defense, _rng);
                DealDamage(target, hit.Amount, hit.Critical);
                if (hit.Critical) target.Knockback(origin, 0.3f);
                yield return new WaitForSeconds(w.Stats.Burst ? 0.04f : 0.16f);
            }
            _player.SpendRounds(w.Stats.Burst ? 1 : plan.Count);
            yield return new WaitForSeconds(w.Stats.RootSeconds * 0.5f);
        }

        /// <summary>Burst Fire: a special attack some rapid-fire weapons (SMG) offer instead of a measured
        /// shot — many rounds at one target, each a coin flip at <see cref="WeaponStats.BurstFireAccuracy"/>,
        /// trading accuracy for volume (Shadowrun Returns-style). Stops early if the target dies.</summary>
        private IEnumerator BurstFireRoutine(EnemyActor chosen)
        {
            _lastAttackAt = Time.time;
            _lastAttackTarget = chosen;
            var w = _player.Weapon;
            var rounds = Mathf.Min(w.Stats.BurstFireRounds, _player.RoundsInClip);
            var share = DamageCalculator.RoundShare(w.Stats.BurstFireRounds);
            _player.Controller.FaceTowards(chosen.transform.position);

            var fired = 0;
            for (var i = 0; i < rounds && chosen.IsAlive; i++)
            {
                fired++;
                _player.Controller.Animator?.SetTrigger(AnimParams.Shoot);
                ShotVisual(chosen);
                if (_rng.Chance(w.Stats.BurstFireAccuracy))
                {
                    var hit = DamageCalculator.Roll(WeaponDamage(), share, chosen.Stats.Defense, _rng);
                    DealDamage(chosen, hit.Amount, hit.Critical);
                    if (hit.Critical) chosen.Knockback(_player.transform.position, 0.2f);
                }
                else
                {
                    GameHud.Instance?.Popup(chosen.AimPoint, "MISS", new Color(0.7f, 0.7f, 0.7f));
                }
                yield return new WaitForSeconds(0.05f);
            }
            _player.SpendRounds(fired);
            yield return new WaitForSeconds(w.Stats.RootSeconds * 0.5f);
        }

        private static readonly Color BladeColor = new Color(0.55f, 0.95f, 1f);

        private void BladeHit(EnemyActor e, Vector3 from, bool rush)
        {
            var hit = DamageCalculator.Roll(BladeArts.Power(BattleConfig.KatanaDamage, _player.Progression.Level, rush), 1f, e.Stats.Defense, _rng);
            DealDamage(e, hit.Amount, hit.Critical);
            BattleFx.Instance?.Burst(e.AimPoint, BladeColor, hit.Critical ? 26 : 14, 5f, 0.05f);
            e.Knockback(from, rush ? 0.3f : 0.45f);
        }

        private IEnumerator SlashRoutine(EnemyActor chosen)
        {
            _lastAttackAt = Time.time;
            _lastAttackTarget = chosen;
            var katana = _player.Katana;
            katana?.Draw();
            katana?.Trail(true);
            _player.Controller.FaceTowards(chosen.transform.position);
            _player.Controller.Animator?.SetTrigger(AnimParams.Slash);
            AudioService.Play(SoundId.KatanaSlash);
            yield return new WaitForSeconds(0.2f);

            var origin = _player.transform.position;
            var facing = _player.transform.forward;
            foreach (var e in _enemies.Where(e => e.IsAlive).ToList())
            {
                var d = e.transform.position - origin;
                if (e == chosen || BladeArts.InSlashArc(facing.x, facing.z, d.x, d.z, BattleConfig.KatanaSlashReach + e.BodyRadius))
                    BladeHit(e, origin, rush: false);
            }
            CameraDirector.Instance?.AddTrauma(0.2f);
            yield return new WaitForSeconds(0.4f);
            katana?.Sheathe();
        }

        private IEnumerator RushRoutine(EnemyActor chosen)
        {
            if (!_player.Pe.TrySpend(BattleConfig.KatanaRushPeCost)) yield break;
            _lastAttackAt = Time.time;
            _lastAttackTarget = chosen;
            var start = _player.transform.position;
            var (ex, ez) = BladeArts.RushEnd(start.x, start.z, chosen.transform.position.x, chosen.transform.position.z);
            yield return DashCut(start, new Vector3(ex, start.y, ez));
        }

        /// <summary>Line Rush: the same dash and cut, but aimed by hand (<see cref="BeginAiming"/>)
        /// instead of locking onto one enemy and auto-extending the line through it.</summary>
        private IEnumerator RushLineRoutine(Vector3 end)
        {
            if (!_player.Pe.TrySpend(BattleConfig.KatanaRushPeCost)) yield break;
            _lastAttackAt = Time.time;
            _lastAttackTarget = null;
            yield return DashCut(_player.transform.position, end);
        }

        private IEnumerator DashCut(Vector3 start, Vector3 end)
        {
            var katana = _player.Katana;
            katana?.Draw();
            katana?.Trail(true);

            if (_zone != null)
            {
                var b = _zone.Bounds;
                end.x = Mathf.Clamp(end.x, b.min.x + 0.6f, b.max.x - 0.6f);
                end.z = Mathf.Clamp(end.z, b.min.z + 0.6f, b.max.z - 0.6f);
            }
            _player.Controller.FaceTowards(end);
            var rotation = _player.transform.rotation;
            _player.Controller.GrantInvulnerability(BattleConfig.KatanaRushSeconds + 0.35f);
            _player.Controller.Animator?.SetTrigger(AnimParams.Rush);
            AudioService.Play(SoundId.KatanaRush);
            BattleFx.Instance?.Tracer(start + Vector3.up * 1.0f, end + Vector3.up * 1.0f, BladeColor);

            var cut = new System.Collections.Generic.HashSet<EnemyActor>();
            for (var t = 0f; t < BattleConfig.KatanaRushSeconds; t += Time.deltaTime)
            {
                var k = 1f - Mathf.Pow(1f - t / BattleConfig.KatanaRushSeconds, 2f);
                var at = Vector3.Lerp(start, end, k);
                _player.Controller.Teleport(at, rotation);
                foreach (var e in _enemies.Where(e => e.IsAlive && !cut.Contains(e)).ToList())
                {
                    var p = e.transform.position;
                    if (!BladeArts.OnRushPath(start.x, start.z, at.x, at.z, p.x, p.z, e.BodyRadius)) continue;
                    cut.Add(e);
                    BladeHit(e, at, rush: true);
                }
                yield return null;
            }
            _player.Controller.Teleport(end, rotation);
            _player.Controller.Animator?.SetTrigger(AnimParams.Slash);
            BattleFx.Instance?.Flash(end + Vector3.up, BladeColor, 6f, 5f, 0.25f);
            CameraDirector.Instance?.AddTrauma(0.35f);
            GameInput.Instance?.Rumble(0.5f, 0.9f, 0.18f);
            if (cut.Count > 1) GameHud.Instance?.Popup(end + Vector3.up * 2.2f, $"{cut.Count} CUT!", new Color(1f, 0.8f, 0.25f), big: true);
            yield return new WaitForSeconds(0.45f);
            katana?.Sheathe();
        }

        private IEnumerator AbilityRoutine(AbilityDefinition ability, EnemyActor target)
        {
            var s = ability.Stats;
            if (!_player.Pe.TrySpend(s.PeCost)) yield break;
            _player.Controller.Animator?.SetTrigger(AnimParams.Cast);
            AudioService.Play(SoundId.PeCast);
            BattleFx.Instance?.Aura(_player.transform, ability.Tint);
            yield return new WaitForSeconds(0.35f);
            var hud = GameHud.Instance;
            switch (s.Effect)
            {
                case AbilityEffect.Heal:
                    var healed = _player.Heal(s.Power);
                    hud?.Popup(_player.transform.position + Vector3.up * 1.8f, $"+{healed}", new Color(0.5f, 1f, 0.6f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case AbilityEffect.EnergyShot:
                    if (target != null && target.IsAlive)
                    {
                        _player.Controller.FaceTowards(target.transform.position);
                        BattleFx.Instance?.Tracer(_player.transform.position + Vector3.up * 1.2f, target.AimPoint, ability.Tint);
                        var hit = DamageCalculator.Roll(s.Power + _player.Progression.Level * 2, 1f, 0, _rng);
                        DealDamage(target, hit.Amount, hit.Critical);
                    }
                    break;
                case AbilityEffect.Haste:
                    _player.ApplyHaste(s.DurationSeconds);
                    hud?.ShowMessage($"Haste: the gauge fills twice as fast for {s.DurationSeconds:0}s.");
                    break;
                case AbilityEffect.Barrier:
                    _player.ApplyBarrier(s.DurationSeconds);
                    hud?.ShowMessage($"Barrier: damage halved for {s.DurationSeconds:0}s.");
                    break;
                case AbilityEffect.Slow:
                    foreach (var e in _enemies.Where(e => e.IsAlive)) e.ApplySlow(s.DurationSeconds);
                    hud?.ShowMessage($"Slow: the creatures move at half speed for {s.DurationSeconds:0}s.");
                    break;
            }
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator UseItemRoutine(ItemDefinition item)
        {
            if (!_player.Bag.TryRemove(item.Id)) yield break;
            var hud = GameHud.Instance;
            switch (item.Stats.Effect)
            {
                case ItemEffect.RestoreHp:
                    var healed = _player.Heal(item.Stats.Power);
                    hud?.Popup(_player.transform.position + Vector3.up * 1.8f, $"+{healed}", new Color(0.5f, 1f, 0.6f));
                    BattleFx.Instance?.Aura(_player.transform, new Color(0.4f, 1f, 0.6f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case ItemEffect.RestorePe:
                    _player.Pe.SetMax(_player.Pe.Max, refill: true);
                    BattleFx.Instance?.Aura(_player.transform, new Color(0.4f, 0.6f, 1f));
                    AudioService.Play(SoundId.Heal);
                    break;
            }
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator ThrowRoutine(ItemDefinition item, EnemyActor target)
        {
            if (!_player.Bag.TryRemove(item.Id)) yield break;
            var start = _player.transform.position + Vector3.up * 1.4f;
            var end = target.transform.position;
            _player.Controller.FaceTowards(end);
            _player.Controller.Animator?.SetTrigger(AnimParams.Throw);
            AudioService.Play(SoundId.Throw);
            yield return new WaitForSeconds(0.25f);

            // A visible grenade on a ballistic arc (scaled time: freezes if the world pauses).
            var grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(grenade.GetComponent<Collider>());
            grenade.transform.localScale = Vector3.one * 0.14f;
            var flight = 0.55f;
            for (var t = 0f; t < flight; t += Time.deltaTime)
            {
                var k = t / flight;
                grenade.transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 2f);
                yield return null;
            }
            Destroy(grenade);

            var radius = item.Stats.Radius;
            if (item.Stats.Effect == ItemEffect.Explosive)
            {
                BattleFx.Instance?.Explosion(end, radius, new Color(1f, 0.55f, 0.15f));
                AudioService.Play(SoundId.Explosion);
                CameraDirector.Instance?.AddTrauma(0.6f);
                GameInput.Instance?.Rumble(1f, 1f, 0.35f);
            }
            else
            {
                BattleFx.Instance?.Flash(end + Vector3.up, Color.white, 12f, radius * 3f, 0.4f);
                BattleFx.Instance?.Burst(end, Color.white, 40, 6f);
                AudioService.Play(SoundId.Explosion, 0.5f);
            }
            foreach (var e in _enemies.Where(e => e.IsAlive && Flat(e.transform.position - end) <= radius + e.BodyRadius).ToList())
            {
                if (item.Stats.Effect == ItemEffect.Explosive)
                {
                    var hit = DamageCalculator.Roll(item.Stats.Power, 1f, e.Stats.Defense, _rng, canCrit: false);
                    DealDamage(e, hit.Amount, false);
                    e.Knockback(end, 0.8f);
                }
                if (item.Stats.StunSeconds > 0f) e.ApplyStun(item.Stats.StunSeconds);
            }
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator ReloadRoutine()
        {
            var hud = GameHud.Instance;
            _activeReload = new ActiveReload(BattleConfig.ActiveReloadDurationSeconds, _rng);
            var reload = _activeReload;
            var pressed = false;
            var perfect = false;
            while (!reload.Finished && !perfect)
            {
                hud?.ShowReload(reload.Progress01, reload.SweetSpotStart01, reload.SweetSpotEnd01);
                // One attempt per reload: a press outside the sweet spot is just absorbed, not re-tried.
                if (!pressed && GameInput.Instance != null && GameInput.Instance.ConfirmPressed)
                {
                    pressed = true;
                    perfect = reload.InSweetSpot;
                }
                if (perfect) break;
                reload.Tick(Time.deltaTime);
                yield return null;
            }
            hud?.ShowReload(-1f, 0f, 0f);
            _activeReload = null;

            if (_player.Reload())
            {
                if (perfect)
                {
                    AudioService.Play(SoundId.ActiveReload);
                    hud?.ShowMessage("Active reload!");
                    hud?.Popup(_player.transform.position + Vector3.up * 1.8f, "ACTIVE RELOAD", new Color(1f, 0.87f, 0.33f));
                    _player.Controller.Unroot();
                }
                else
                {
                    AudioService.Play(SoundId.Reload);
                    hud?.ShowMessage("Reloaded.");
                }
            }
        }
    

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
            if (Phase == BattlePhase.Command || Phase == BattlePhase.Targeting) ResumeRealtime(spendTurn: false);
        }

        public bool AutoConfirmTarget()
        {
            if (Phase != BattlePhase.Targeting || _targets.Count == 0) return false;
            var target = _targets[_targetIndex];
            _dome.Hide();
            _onTargetChosen?.Invoke(target);
            return true;
        }

        /// <summary>Opens a Focus window against the nearest enemy, as if a perfect dodge had happened.</summary>
        public bool DebugOpenFocus()
        {
            if (Phase != BattlePhase.Active) return false;
            _focusAttacker = _enemies.Where(e => e.IsAlive)
                .OrderBy(e => (e.transform.position - _player.transform.position).sqrMagnitude).FirstOrDefault();
            if (_focusAttacker == null) return false;
            _focus.Open();
            AudioService.Play(SoundId.PerfectDodge);
            GameHud.Instance?.ShowFocus(1f);
            return true;
        }
    }
}
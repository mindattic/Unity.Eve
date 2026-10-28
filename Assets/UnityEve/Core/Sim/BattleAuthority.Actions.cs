using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Session;

namespace Unity.Eve.Core.Sim
{
    /// <summary>The paused half of the battle: the command menu's orders, and each turn as a routine.</summary>
    public sealed partial class BattleAuthority
    {
        // ------------------------------------------------------------------ orders

        /// <summary>Applies one order from the seat. Returns null when applied, or why it was refused.</summary>
        public string Apply(BattleCommand c)
        {
            if (c == null) return "no command";
            if (!InBattle) return "no battle is running";
            if (c.Player != Player.Seat) return $"{c.Player} is not in this battle";

            switch (c.Order)
            {
                case BattleOrder.OpenMenu:
                    return OpenCommandMenu() ? null : "the gauge is not full";
                case BattleOrder.FocusMove:
                    if (c.Index < 0 || c.Index > (int)FocusMove.Riposte) return "no such Focus move";
                    return UseFocusMove((FocusMove)c.Index) ? null : "no Focus window is open";
                case BattleOrder.DebugFocus:
                    return DebugOpenFocus() ? null : "nothing to focus on";
            }

            if (Stage != BattleStage.Command) return "the command menu is not open";
            var w = State.Weapon;
            var target = c.Target >= 0 ? FindEnemy(c.Target) : null;
            switch (c.Order)
            {
                case BattleOrder.CancelMenu:
                    ResumeRealtime(spendTurn: false);
                    return null;
                case BattleOrder.Equip:
                    if (c.Index < 0 || c.Index >= State.Weapons.Count) return "no such weapon";
                    State.Equip(c.Index);
                    return null;
                case BattleOrder.Escape:
                    DoEscape();
                    return null;
                case BattleOrder.Reload:
                    if (!State.CanReload) return "nothing to reload with";
                    Execute(ReloadRoutine(), BattleConfig.ActiveReloadDurationSeconds);
                    return null;
                case BattleOrder.Attack:
                    if (w == null) return "no weapon";
                    if (!InReach(target, w.Range)) return "target not in range";
                    Execute(AttackRoutine(target), w.RootSeconds + 0.1f);
                    return null;
                case BattleOrder.BurstFire:
                    if (w == null || w.BurstFireRounds <= 0 || State.RoundsInClip <= 0) return "no burst to fire";
                    if (!InReach(target, w.Range)) return "target not in range";
                    Execute(BurstFireRoutine(target), w.RootSeconds + 0.3f);
                    return null;
                case BattleOrder.Slash:
                    if (!InReach(target, BattleConfig.KatanaSlashReach)) return "target not in reach";
                    Execute(SlashRoutine(target), 0.75f);
                    return null;
                case BattleOrder.Rush:
                    if (!State.Pe.CanAfford(BattleConfig.KatanaRushPeCost)) return "not enough PE";
                    if (!InReach(target, BattleConfig.KatanaRushRange)) return "target not in range";
                    Execute(RushRoutine(target), 0.9f);
                    return null;
                case BattleOrder.LineRush:
                {
                    if (!State.Pe.CanAfford(BattleConfig.KatanaRushPeCost)) return "not enough PE";
                    var end = new Vec3(c.X, Player.Position.Y, c.Z);
                    if ((end - Player.Position).Flat.Magnitude > BattleConfig.KatanaRushRange + 0.01f) return "aimed past the rush range";
                    Execute(RushLineRoutine(end), 0.9f);
                    return null;
                }
                case BattleOrder.Ability:
                {
                    if (c.Index < 0 || c.Index >= State.Abilities.Count) return "no such ability";
                    var a = State.Abilities[c.Index];
                    if (!State.Pe.CanAfford(a.PeCost)) return "not enough PE";
                    if (a.NeedsTarget && !InReach(target, a.Range)) return "target not in range";
                    Execute(AbilityRoutine(c.Index, a.NeedsTarget ? target : null), 0.8f);
                    return null;
                }
                case BattleOrder.Item:
                {
                    var item = State.FindItem(c.Item);
                    if (item == null || !item.UsableInBattle || State.Bag.CountOf(item.Id) <= 0) return "no such item to use";
                    switch (item.Effect)
                    {
                        case ItemEffect.Explosive:
                        case ItemEffect.Flash:
                            if (!InReach(target, _throwRange)) return "target not in throwing range";
                            Execute(ThrowRoutine(item, target), 0.9f);
                            return null;
                        default:
                            Execute(UseItemRoutine(item), 0.6f);
                            return null;
                    }
                }
                default:
                    return $"unknown order {c.Order}";
            }
        }

        /// <summary>The same test the range dome uses: alive, and within range plus its body on the ground plane.</summary>
        private bool InReach(EnemyState target, float range) =>
            target != null && target.IsAlive && (target.Position - Player.Position).Flat.Magnitude <= range + target.BodyRadius;

        /// <summary>Stops time for the command menu (the self-play opens it directly; the seat by pressing Confirm).</summary>
        public bool OpenCommandMenu()
        {
            if (Stage != BattleStage.Active || !_atb.IsFull || _focus.IsOpen) return false;
            Stage = BattleStage.Command;
            Player.Lock();
            _menuLocked = true;
            Raise(BattleCue.MenuOpened);
            return true;
        }

        private void CloseMenus()
        {
            Raise(BattleCue.MenuClosed);
            if (!_menuLocked) return;
            _menuLocked = false;
            Player.Unlock();
            // The button that closed the menu must not also dodge, nor count as a press inside the action.
            SuppressPresses?.Invoke(Player.Seat, IntentButtons.Cancel | IntentButtons.Confirm);
        }

        private void ResumeRealtime(bool spendTurn)
        {
            CloseMenus();
            if (spendTurn) _atb.Spend();
            Stage = BattleStage.Active;
        }

        private void DoEscape()
        {
            var strongest = _enemies.Where(e => e.IsAlive).Select(e => e.Stats.Level).DefaultIfEmpty(1).Max();
            if (_rng.Chance(EscapeRule.Chance(State.Progression.Level, strongest)))
            {
                ResumeRealtime(spendTurn: true);
                Raise(BattleCue.Escaped);
                End(won: false);
            }
            else
            {
                Raise(BattleCue.EscapeFailed);
                ResumeRealtime(spendTurn: true);
            }
        }

        /// <summary>Leaves the pause, roots her and plays the turn out in real time.</summary>
        private void Execute(IEnumerator routine, float rootSeconds)
        {
            CloseMenus();
            Player.Root(rootSeconds);
            _atb.Spend();
            Stage = BattleStage.Executing;
            _routines.Start(ExecuteRoutine(routine));
        }

        private IEnumerator ExecuteRoutine(IEnumerator routine)
        {
            yield return routine;
            if (Stage == BattleStage.Executing) Stage = BattleStage.Active;
        }

        private void Motion(PlayerMotion motion) => Raise(new BattleEvent(BattleCue.Motion) { Motion = motion });

        // ------------------------------------------------------------------ turns

        private IEnumerator AttackRoutine(EnemyState chosen)
        {
            _lastAttackAt = Now;
            _lastAttackTarget = chosen;
            var w = State.Weapon;
            var origin = Player.Position;
            var inRange = _enemies
                .Where(e => e.IsAlive && (e.Position - origin).Flat.Magnitude <= w.Range + e.BodyRadius)
                .OrderBy(e => e == chosen ? 0 : 1).ToList();
            var plan = DamageCalculator.PlanRounds(w, 0, inRange.Count, w.Melee ? int.MaxValue : State.RoundsInClip);
            var share = DamageCalculator.RoundShare(w.RoundsPerTurn);
            Player.FaceTowards(chosen.Position);

            foreach (var index in plan)
            {
                var target = inRange[index];
                if (!target.IsAlive) continue;
                Player.FaceTowards(target.Position);
                Motion(PlayerMotion.Shoot);
                if (!w.Melee) Raise(BattleCue.Shot, target);
                var hit = DamageCalculator.Roll(WeaponDamage(), share, target.Stats.Defense, _rng);
                DealDamage(target, hit.Amount, hit.Critical);
                if (hit.Critical) target.Knockback(origin, 0.3f);
                yield return new Wait(w.Burst ? 0.04f : 0.16f);
            }
            State.SpendRounds(w.Burst ? 1 : plan.Count);
            yield return new Wait(w.RootSeconds * 0.5f);
        }

        /// <summary>Burst Fire: many rounds at one target, each a coin flip at the weapon's burst accuracy,
        /// trading accuracy for volume. Stops early if the target dies.</summary>
        private IEnumerator BurstFireRoutine(EnemyState chosen)
        {
            _lastAttackAt = Now;
            _lastAttackTarget = chosen;
            var w = State.Weapon;
            var rounds = Math.Min(w.BurstFireRounds, State.RoundsInClip);
            var share = DamageCalculator.RoundShare(w.BurstFireRounds);
            Player.FaceTowards(chosen.Position);

            var fired = 0;
            for (var i = 0; i < rounds && chosen.IsAlive; i++)
            {
                fired++;
                Motion(PlayerMotion.Shoot);
                Raise(BattleCue.Shot, chosen);
                if (_rng.Chance(w.BurstFireAccuracy))
                {
                    var hit = DamageCalculator.Roll(WeaponDamage(), share, chosen.Stats.Defense, _rng);
                    DealDamage(chosen, hit.Amount, hit.Critical);
                    if (hit.Critical) chosen.Knockback(Player.Position, 0.2f);
                }
                else
                {
                    Raise(BattleCue.ShotMissed, chosen);
                }
                yield return new Wait(0.05f);
            }
            State.SpendRounds(fired);
            yield return new Wait(w.RootSeconds * 0.5f);
        }

        private void BladeHit(EnemyState e, Vec3 from, bool rush)
        {
            var hit = DamageCalculator.Roll(BladeArts.Power(BattleConfig.KatanaDamage, State.Progression.Level, rush), 1f, e.Stats.Defense, _rng);
            DealDamage(e, hit.Amount, hit.Critical);
            Raise(BattleCue.BladeHit, e, flag: hit.Critical);
            e.Knockback(from, rush ? 0.3f : 0.45f);
        }

        private IEnumerator SlashRoutine(EnemyState chosen)
        {
            _lastAttackAt = Now;
            _lastAttackTarget = chosen;
            Raise(BattleCue.KatanaDrawn);
            Player.FaceTowards(chosen.Position);
            Raise(BattleCue.SlashSwing);
            yield return new Wait(0.2f);

            var origin = Player.Position;
            var facing = Player.Forward;
            foreach (var e in _enemies.Where(e => e.IsAlive).ToList())
            {
                var d = e.Position - origin;
                if (e == chosen || BladeArts.InSlashArc(facing.X, facing.Z, d.X, d.Z, BattleConfig.KatanaSlashReach + e.BodyRadius))
                    BladeHit(e, origin, rush: false);
            }
            Raise(BattleCue.SlashLanded);
            yield return new Wait(0.4f);
            Raise(BattleCue.KatanaSheathed);
        }

        private IEnumerator RushRoutine(EnemyState chosen)
        {
            if (!State.Pe.TrySpend(BattleConfig.KatanaRushPeCost)) yield break;
            _lastAttackAt = Now;
            _lastAttackTarget = chosen;
            var start = Player.Position;
            var (ex, ez) = BladeArts.RushEnd(start.X, start.Z, chosen.Position.X, chosen.Position.Z);
            yield return DashCut(start, new Vec3(ex, start.Y, ez));
        }

        /// <summary>Line Rush: the same dash and cut, aimed by hand instead of locking onto one creature.</summary>
        private IEnumerator RushLineRoutine(Vec3 end)
        {
            if (!State.Pe.TrySpend(BattleConfig.KatanaRushPeCost)) yield break;
            _lastAttackAt = Now;
            _lastAttackTarget = null;
            yield return DashCut(Player.Position, end);
        }

        private IEnumerator DashCut(Vec3 start, Vec3 end)
        {
            Raise(BattleCue.KatanaDrawn);
            end = new Vec3(
                SimMath.Clamp(end.X, _arena.Min.X + 0.6f, _arena.Max.X - 0.6f), end.Y,
                SimMath.Clamp(end.Z, _arena.Min.Z + 0.6f, _arena.Max.Z - 0.6f));
            Player.FaceTowards(end);
            var yaw = Player.Yaw;
            Player.GrantInvulnerability(BattleConfig.KatanaRushSeconds + 0.35f);
            Raise(new BattleEvent(BattleCue.RushBegan) { At = start, To = end });

            var cut = new HashSet<EnemyState>();
            for (var t = 0f; t < BattleConfig.KatanaRushSeconds; t += _clock.Delta)
            {
                var k = 1f - (float)Math.Pow(1f - t / BattleConfig.KatanaRushSeconds, 2f);
                var at = Vec3.Lerp(start, end, k);
                Player.Teleport(at, yaw);
                foreach (var e in _enemies.Where(e => e.IsAlive && !cut.Contains(e)).ToList())
                {
                    var p = e.Position;
                    if (!BladeArts.OnRushPath(start.X, start.Z, at.X, at.Z, p.X, p.Z, e.BodyRadius)) continue;
                    cut.Add(e);
                    BladeHit(e, at, rush: true);
                }
                yield return null;
            }
            Player.Teleport(end, yaw);
            Raise(new BattleEvent(BattleCue.RushEnded) { To = end, Amount = cut.Count });
            yield return new Wait(0.45f);
            Raise(BattleCue.KatanaSheathed);
        }

        private IEnumerator AbilityRoutine(int index, EnemyState target)
        {
            var s = State.Abilities[index];
            if (!State.Pe.TrySpend(s.PeCost)) yield break;
            Raise(new BattleEvent(BattleCue.Cast) { Index = index });
            yield return new Wait(0.35f);
            switch (s.Effect)
            {
                case AbilityEffect.Heal:
                    var healed = State.Heal(s.Power);
                    Raise(BattleCue.AbilityHealed, amount: healed);
                    break;
                case AbilityEffect.EnergyShot:
                    if (target != null && target.IsAlive)
                    {
                        Player.FaceTowards(target.Position);
                        Raise(new BattleEvent(BattleCue.EnergyShot) { Enemy = target, Index = index });
                        var hit = DamageCalculator.Roll(s.Power + State.Progression.Level * 2, 1f, 0, _rng);
                        DealDamage(target, hit.Amount, hit.Critical);
                    }
                    break;
                case AbilityEffect.Haste:
                    State.ApplyHaste(s.DurationSeconds);
                    Raise(new BattleEvent(BattleCue.Hasted) { Value = s.DurationSeconds });
                    break;
                case AbilityEffect.Barrier:
                    State.ApplyBarrier(s.DurationSeconds);
                    Raise(new BattleEvent(BattleCue.Barriered) { Value = s.DurationSeconds });
                    break;
                case AbilityEffect.Slow:
                    foreach (var e in _enemies.Where(e => e.IsAlive)) e.ApplySlow(s.DurationSeconds);
                    Raise(new BattleEvent(BattleCue.Slowed) { Value = s.DurationSeconds });
                    break;
            }
            yield return new Wait(0.3f);
        }

        private IEnumerator UseItemRoutine(ItemStats item)
        {
            if (!State.Bag.TryRemove(item.Id)) yield break;
            switch (item.Effect)
            {
                case ItemEffect.RestoreHp:
                    var healed = State.Heal(item.Power);
                    Raise(BattleCue.ItemHealed, amount: healed);
                    break;
                case ItemEffect.RestorePe:
                    State.Pe.SetMax(State.Pe.Max, refill: true);
                    Raise(BattleCue.PeRestored);
                    break;
            }
            yield return new Wait(0.4f);
        }

        private IEnumerator ThrowRoutine(ItemStats item, EnemyState target)
        {
            if (!State.Bag.TryRemove(item.Id)) yield break;
            var start = Player.Position + Vec3.Up * 1.4f;
            var end = target.Position;
            Player.FaceTowards(end);
            Raise(BattleCue.ThrowStarted);
            yield return new Wait(0.25f);

            // On a ballistic arc in scaled time: it freezes if the world pauses.
            const float flight = 0.55f;
            Raise(new BattleEvent(BattleCue.Thrown) { At = start, To = end, Value = flight });
            for (var t = 0f; t < flight; t += _clock.Delta) yield return null;

            var radius = item.Radius;
            var explosive = item.Effect == ItemEffect.Explosive;
            Raise(new BattleEvent(BattleCue.Burst) { To = end, Value = radius, Flag = explosive });
            foreach (var e in _enemies.Where(e => e.IsAlive && (e.Position - end).Flat.Magnitude <= radius + e.BodyRadius).ToList())
            {
                if (explosive)
                {
                    var hit = DamageCalculator.Roll(item.Power, 1f, e.Stats.Defense, _rng, canCrit: false);
                    DealDamage(e, hit.Amount, false);
                    e.Knockback(end, 0.8f);
                }
                if (item.StunSeconds > 0f) e.ApplyStun(item.StunSeconds);
            }
            yield return new Wait(0.3f);
        }

        private IEnumerator ReloadRoutine()
        {
            var reload = new ActiveReload(BattleConfig.ActiveReloadDurationSeconds, _rng);
            CurrentReload = reload;
            var pressed = false;
            var perfect = false;
            while (!reload.Finished && !perfect)
            {
                // One attempt per reload: a press outside the sweet spot is just absorbed, not re-tried.
                if (!pressed && _input.ConfirmPressed)
                {
                    pressed = true;
                    perfect = reload.InSweetSpot;
                }
                if (perfect) break;
                reload.Tick(_clock.Delta);
                yield return null;
            }
            CurrentReload = null;

            if (State.Reload())
            {
                Raise(BattleCue.Reloaded, flag: perfect);
                if (perfect) Player.Unroot();
            }
        }
    }
}

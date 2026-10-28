using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Session;
using Unity.Eve.Core.Story;
using Unity.Eve.Core.Util;

namespace Unity.Eve.Core.Sim
{
    /// <summary>Where a battle stands. Picking a target or aiming is presentation inside <see cref="Command"/>.</summary>
    public enum BattleStage
    {
        Inactive,
        /// <summary>Real time: the ATB fills, the player moves and dodges, creatures attack.</summary>
        Active,
        /// <summary>World paused: the seat is choosing an action.</summary>
        Command,
        /// <summary>Real time, player rooted: the chosen action plays out.</summary>
        Executing,
        Victory,
        Defeat,
    }

    /// <summary>One creature to materialize when a battle begins.</summary>
    public readonly struct EnemySpawn
    {
        public EnemySpawn(EnemyKind kind, Vec3 position)
        {
            Kind = kind;
            Position = position;
        }

        public EnemyKind Kind { get; }
        public Vec3 Position { get; }
    }

    /// <summary>Everything a battle starts from besides the player: with the same setup, seed and command log
    /// it plays out the same.</summary>
    public sealed class BattleSetup
    {
        public uint Seed;
        public Arena Arena;
        /// <summary>Spawn points the encounter lists (sizes the pack's attack tokens).</summary>
        public int PackSize;
        public readonly List<EnemySpawn> Spawns = new List<EnemySpawn>();
        /// <summary>How far she can throw (the item range dome).</summary>
        public float ThrowRange = 10f;
    }

    /// <summary>
    /// Parasite Eve's Active Time Battle plus the optional Focus layer, as the authority (U.EVE-LAW-6): all the
    /// battle's state and rules, stepped at the fixed tick from the seat's input and commands, raising
    /// <see cref="BattleEvent"/>s for presentation. The world runs in real time while the ATB gauge fills; a full
    /// gauge lets the seat stop time and pick an action (a <see cref="BattleCommand"/>), which then plays out in
    /// real time as a routine. A perfectly timed dodge opens a slow-motion Focus window with a free move.
    /// Every roll comes from the battle's seeded <see cref="Rng"/>.
    /// </summary>
    public sealed partial class BattleAuthority : IThreats
    {
        private readonly SimClock _clock;
        private readonly RoutineRunner _routines;
        private readonly List<EnemyState> _enemies = new List<EnemyState>();
        private readonly AtbGauge _atb = new AtbGauge();
        private readonly FocusWindow _focus = new FocusWindow();
        private readonly List<Puddle> _puddles = new List<Puddle>();
        private Rng _rng = new Rng(1u);
        private Arena _arena;
        private float _throwRange = 10f;
        private AttackTokens _tokens;
        private BossPhases _bossPhases;
        private EnemyState _focusAttacker;
        private int _expEarned;
        private bool _menuLocked;
        private float _lastAttackAt = -999f;
        private EnemyState _lastAttackTarget;
        private float _shoveReadyAt = -999f;
        private bool _wasShootdodging;
        private bool _bulletTime;
        private int _nextEnemyId;
        private int _nextPuddleId;
        private TickInput _input = TickInput.None;

        private sealed class Puddle
        {
            public int Id;
            public Vec3 At;
            public float DiesAt;
            public float NextTickAt;
        }

        public BattleAuthority(SimClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _routines = new RoutineRunner(clock);
            _atb.Filled += () => Raise(BattleCue.AtbFilled);
            _focus.Opened += () => Raise(BattleCue.FocusOpened);
            _focus.Closed += () => Raise(BattleCue.FocusClosed);
        }

        /// <summary>Level geometry queries (cover); nothing gives cover without one.</summary>
        public IWorldQueries World { get; set; } = OpenWorld.Instance;
        /// <summary>Drops presses a menu already used up from a seat's next tick (live input only).</summary>
        public Action<PlayerId, IntentButtons> SuppressPresses { get; set; }

        public BattleStage Stage { get; private set; } = BattleStage.Inactive;
        public bool InBattle => Stage != BattleStage.Inactive;
        public PlayerBody Player { get; private set; }
        public PlayerState State => Player?.State;
        public AtbGauge Atb => _atb;
        public FocusWindow Focus => _focus;
        public IReadOnlyList<EnemyState> Enemies => _enemies;
        public Arena Arena => _arena;
        public float ThrowRange => _throwRange;
        public bool BossFight => _bossPhases != null;
        public int PerfectDodges => _focus.PerfectDodges;
        /// <summary>The live active-reload minigame while one is running; null the rest of the time.</summary>
        public ActiveReload CurrentReload { get; private set; }
        public float SecondsSinceLastAttack => _clock.Now - _lastAttackAt;
        public EnemyState LastAttackTarget => _lastAttackTarget;
        /// <summary>Routines still playing out (a turn, the victory beat).</summary>
        public int RunningRoutines => _routines.Count;

        /// <summary>The world speed this battle asks for: stopped in the menu, slowed in Focus and bullet time.
        /// (In the game <c>WorldTime</c> applies it from the cues; tests and replays read it here.)</summary>
        public float SuggestedTimeScale
        {
            get
            {
                if (Stage == BattleStage.Command) return 0f;
                var scale = 1f;
                if (_focus.IsOpen) scale = Math.Min(scale, FocusWindow.SlowTimeScale);
                if (_bulletTime) scale = Math.Min(scale, BattleConfig.ShootdodgeTimeScale);
                return scale;
            }
        }

        public event Action<BattleEvent> EventRaised;

        private float Now => _clock.Now;

        private BattleEvent Raise(BattleCue cue, EnemyState enemy = null, int amount = 0, bool flag = false)
        {
            var e = new BattleEvent(cue) { Enemy = enemy, Amount = amount, Flag = flag };
            if (Player != null) e.Player = Player.Seat;
            EventRaised?.Invoke(e);
            return e;
        }

        private void Raise(BattleEvent e)
        {
            if (Player != null) e.Player = Player.Seat;
            EventRaised?.Invoke(e);
        }

        public EnemyState FindEnemy(int id)
        {
            foreach (var e in _enemies)
                if (e.Id == id) return e;
            return null;
        }

        public EnemyState NearestLivingEnemy(Vec3 from)
        {
            if (!InBattle) return null;
            EnemyState nearest = null;
            var best = float.MaxValue;
            foreach (var e in _enemies)
            {
                if (!e.IsAlive) continue;
                var sqr = (e.Position - from).SqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = e;
                }
            }
            return nearest;
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>
        /// Materializes the encounter around <paramref name="player"/>. The seed's draw order is fixed: the
        /// tokens' own seed, then for each spawn its start delay, then its creature's seed.
        /// </summary>
        public bool Begin(PlayerBody player, BattleSetup setup)
        {
            if (InBattle || player == null || player.State.IsDead || setup == null) return false;
            Player = player;
            player.Threats = this;
            _rng = new Rng(setup.Seed);
            _arena = setup.Arena;
            _throwRange = setup.ThrowRange;
            _expEarned = 0;
            _enemies.Clear();
            _routines.StopAll();
            _atb.Spend();
            State.Pe.BeginBattle();
            NudgeInside();

            // Attack tokens: the pack takes turns engaging her instead of mobbing her.
            _tokens = new AttackTokens(AttackTokens.CapacityFor(setup.PackSize), new Rng(_rng.NextUInt()), firstDelay: 1.2f);
            var i = 0;
            foreach (var spawn in setup.Spawns)
            {
                Spawn(spawn.Kind, spawn.Position, 0.6f + _rng.Range(0f, 0.8f) + i * 0.25f);
                i++;
            }
            var boss = _enemies.FirstOrDefault(e => e.Stats.Boss);
            _bossPhases = boss != null ? new BossPhases(0.66f, 0.33f) : null;

            Stage = BattleStage.Active;
            Raise(BattleCue.Began, amount: _enemies.Count);
            return true;
        }

        private EnemyState Spawn(EnemyKind kind, Vec3 at, float initialCooldown)
        {
            var toPlayer = (Player.Position - at).Flat;
            var yaw = toPlayer.SqrMagnitude > 0f ? SimMath.YawOf(toPlayer) : 0f;
            var enemy = new EnemyState(_nextEnemyId++, kind, at, yaw, _clock);
            enemy.Begin(Player, MayCommit, initialCooldown, _rng.NextUInt(), _arena,
                () => _enemies.Count(e => e.IsAlive), _tokens, OnEnemyStrike, OnEnemyDied);
            _enemies.Add(enemy);
            Raise(BattleCue.EnemySpawned, enemy);
            return enemy;
        }

        /// <summary>The boss calls its brood as its health falls (BossPhases decides when).</summary>
        private void CheckBossPhases()
        {
            if (_bossPhases == null) return;
            var boss = _enemies.FirstOrDefault(e => e.Stats.Boss);
            if (boss == null || !boss.IsAlive || boss.Kind.Brood == null) return;
            var waves = _bossPhases.Check(boss.Health.Fraction);
            if (waves <= 0) return;
            var min = _arena.Min;
            var max = _arena.Max;
            for (var w = 0; w < waves; w++)
            foreach (var corner in new[] { new Vec3(min.X + 1.5f, 0f, max.Z - 1.5f), new Vec3(max.X - 1.5f, 0f, max.Z - 1.5f) })
                Spawn(boss.Kind.Brood, new Vec3(corner.X, Player.Position.Y, corner.Z), 1.2f);
            Raise(BattleCue.BroodCalled);
        }

        private void NudgeInside()
        {
            var p = Player.Position;
            var inner = new Vec3(
                SimMath.Clamp(p.X, _arena.Min.X + 0.8f, _arena.Max.X - 0.8f), p.Y,
                SimMath.Clamp(p.Z, _arena.Min.Z + 0.8f, _arena.Max.Z - 0.8f));
            if ((inner - p).SqrMagnitude > 0.0001f) Player.Teleport(inner, Player.Yaw);
        }

        private bool MayCommit() => _enemies.Count(e => e.IsAlive && e.IsCommitted) < BattleConfig.MaxCommittedAttackers;

        private void End(bool won, bool defeated = false)
        {
            CloseMenus();
            _focus.Close();
            _bulletTime = false;
            _wasShootdodging = false;
            foreach (var p in _puddles) Raise(new BattleEvent(BattleCue.PuddleGone) { Index = p.Id });
            _puddles.Clear();
            _enemies.Clear();
            State.ClearBuffs();
            Stage = BattleStage.Inactive;
            Raise(new BattleEvent(BattleCue.Ended) { Flag = won, Index = defeated ? 1 : 0 });
        }

        // ------------------------------------------------------------------ per tick

        /// <summary>One fixed tick of the battle, after the seat's body has stepped.</summary>
        /// <param name="dt">Scaled tick seconds.</param>
        /// <param name="realDt">The tick's real seconds (the Focus window counts these).</param>
        public void Tick(in TickInput input, float dt, float realDt)
        {
            if (!InBattle) return;
            _input = input;
            _focus.Tick(realDt);

            switch (Stage)
            {
                case BattleStage.Active:
                    _tokens?.Tick(dt);
                    TickRealtime(dt);
                    if (_focus.IsOpen) HandleFocusInput(input);
                    else if (input.ConfirmPressed && _atb.IsFull && Player.CanAct) OpenCommandMenu();
                    if (!_focus.IsOpen && input.InteractPressed && Now >= _shoveReadyAt && Player.CanAct) DoShove();
                    if (!_focus.IsOpen && input.ShootdodgePressed && CanShootdodge()) DoShootdodge();
                    if (Player.IsShootdodging && input.ConfirmPressed) FireDuringShootdodge();
                    TrackShootdodgeLanding();
                    break;
                case BattleStage.Executing:
                    TickRealtime(dt);
                    break;
            }

            // Creatures (a brood called up below joins from the next tick).
            for (int i = 0, n = _enemies.Count; i < n && i < _enemies.Count; i++) _enemies[i].Tick(dt);
            if (!InBattle) return;

            EnemyState.Separate(_enemies, Player.Position);
            if (Stage == BattleStage.Active || Stage == BattleStage.Executing) CheckBossPhases();
            CheckOutcome();
            TickPuddles();
            _routines.Tick();
            _input = TickInput.None;
        }

        private void TickRealtime(float dt)
        {
            _atb.Tick(dt, State.AtbRate, State.Hasted);
            State.Pe.Regenerate(dt);
        }

        private void CheckOutcome()
        {
            if (Stage == BattleStage.Victory || Stage == BattleStage.Defeat || !InBattle) return;
            if (State.IsDead && Stage != BattleStage.Executing)
            {
                Stage = BattleStage.Defeat;
                CloseMenus();
                _focus.Close();
                _routines.Start(DefeatRoutine());
                return;
            }
            if (_enemies.Count > 0 && _enemies.TrueForAll(e => !e.IsAlive) && Stage != BattleStage.Executing)
            {
                Stage = BattleStage.Victory;
                CloseMenus();
                _focus.Close();
                _routines.Start(VictoryRoutine());
            }
        }

        private IEnumerator VictoryRoutine()
        {
            Raise(BattleCue.VictoryBegan);
            yield return new Wait(0.6f);
            var levels = State.GainExp(_expEarned);
            Raise(BattleCue.Victory, amount: _expEarned);
            if (levels > 0)
            {
                yield return new Wait(0.9f);
                Raise(BattleCue.LevelUp, amount: State.Progression.Level);
            }
            yield return new Wait(1.2f);
            End(won: true);
        }

        private IEnumerator DefeatRoutine()
        {
            Raise(new BattleEvent(BattleCue.Motion) { Motion = PlayerMotion.Die });
            Raise(BattleCue.Defeated);
            yield return new Wait(1.5f);
            End(won: false, defeated: true);
        }

        // ------------------------------------------------------------------ puddles

        private void SpawnPuddle(Vec3 at)
        {
            var p = new Puddle
            {
                Id = ++_nextPuddleId,
                At = at,
                DiesAt = Now + BattleConfig.PuddleDurationSeconds,
                // The first burn comes one full tick interval after the spit that made it, never in the same instant.
                NextTickAt = Now + BattleConfig.PuddleTickSeconds,
            };
            _puddles.Add(p);
            Raise(new BattleEvent(BattleCue.PuddleSpawned) { Index = p.Id, At = at });
        }

        private void TickPuddles()
        {
            for (var i = 0; i < _puddles.Count; i++)
            {
                var p = _puddles[i];
                if (Now >= p.DiesAt || !InBattle)
                {
                    _puddles.RemoveAt(i--);
                    Raise(new BattleEvent(BattleCue.PuddleGone) { Index = p.Id });
                    continue;
                }
                if (State.IsDead || Now < p.NextTickAt) continue;
                if ((p.At - Player.Position).Flat.Magnitude > BattleConfig.PuddleRadius) continue;
                p.NextTickAt = Now + BattleConfig.PuddleTickSeconds;
                var dealt = State.TakeDamage(BattleConfig.PuddleDamagePerTick);
                if (dealt > 0) Raise(BattleCue.PuddleBurned, amount: dealt);
            }
        }

        // ------------------------------------------------------------------ enemy strikes and Focus

        private void OnEnemyStrike(EnemyState enemy)
        {
            if (!InBattle || !enemy.IsAlive) return;
            var d = (enemy.Position - Player.Position).Flat;
            var outcome = EnemyAttackCycle.Resolve(d.Magnitude, enemy.Stats.Reach, Player.IsInvulnerable);
            switch (outcome)
            {
                case StrikeOutcome.Evaded:
                    if (FocusWindow.IsPerfect(Player.SecondsSinceDodge) && Stage == BattleStage.Active)
                    {
                        _focusAttacker = enemy;
                        _focus.Open();
                        Raise(BattleCue.PerfectDodge, enemy);
                    }
                    else
                    {
                        Raise(BattleCue.StrikeEvaded, enemy);
                    }
                    break;
                case StrikeOutcome.Hit:
                    var exposure = World.CoverExposure(Player.Position, enemy.AimPoint);
                    var inCover = exposure > 0.05f;
                    var raw = enemy.Stats.AttackDamage * enemy.DamageMultiplier * _rng.Range(0.85f, 1.15f);
                    raw *= 1f - exposure * BattleConfig.CoverMaxReduction;
                    var dealt = State.TakeDamage((int)Math.Round(raw));
                    Raise(BattleCue.PlayerHit, enemy, dealt, inCover);
                    if (enemy.Stats.LeavesPuddle) SpawnPuddle(Player.Position);
                    break;
            }
        }

        private void HandleFocusInput(in TickInput input)
        {
            if (input.FocusHealPressed) UseFocusMove(FocusMove.QuickHeal);
            else if (input.FocusVitalsPressed) UseFocusMove(FocusMove.TargetVitals);
            else if (input.FocusRipostePressed) UseFocusMove(FocusMove.Riposte);
        }

        /// <summary>Spends the open Focus window on a free move. No ATB cost.</summary>
        public bool UseFocusMove(FocusMove move)
        {
            if (_focusAttacker == null || !_focus.TryUse()) return false;
            switch (move)
            {
                case FocusMove.QuickHeal:
                    var healed = State.Heal(FocusWindow.QuickHealAmount);
                    Raise(BattleCue.FocusHealed, amount: healed);
                    break;
                case FocusMove.TargetVitals:
                    if (!_focusAttacker.IsAlive) break;
                    Player.FaceTowards(_focusAttacker.Position);
                    Raise(new BattleEvent(BattleCue.Motion) { Motion = PlayerMotion.Shoot });
                    var hit = DamageCalculator.Roll(WeaponDamage(), FocusWindow.VitalsMultiplier, _focusAttacker.Stats.Defense, _rng, canCrit: false);
                    Raise(BattleCue.Shot, _focusAttacker);
                    DealDamage(_focusAttacker, hit.Amount, critical: true, label: "VITALS");
                    break;
                case FocusMove.Riposte:
                    if (!_focusAttacker.IsAlive) break;
                    _focusAttacker.Knockback(Player.Position, BattleConfig.ShoveKnockbackMetres);
                    _focusAttacker.ApplyStun(FocusWindow.RiposteStunSeconds);
                    Raise(new BattleEvent(BattleCue.Motion) { Motion = PlayerMotion.Shove });
                    DealDamage(_focusAttacker, WeaponDamage(), critical: true, label: "RIPOSTE");
                    break;
            }
            return true;
        }

        /// <summary>Opens a Focus window against the nearest creature, as if a perfect dodge had happened (self-play).</summary>
        public bool DebugOpenFocus()
        {
            if (Stage != BattleStage.Active) return false;
            _focusAttacker = _enemies.Where(e => e.IsAlive)
                .OrderBy(e => (e.Position - Player.Position).SqrMagnitude).FirstOrDefault();
            if (_focusAttacker == null) return false;
            _focus.Open();
            Raise(BattleCue.PerfectDodge, _focusAttacker, flag: true);
            return true;
        }

        /// <summary>Free melee (Interact, no ATB cost, no ammo): a short-reach shove that knocks back and
        /// staggers whatever's in front of her. Control, not damage.</summary>
        private void DoShove()
        {
            var origin = Player.Position;
            var facing = Player.Forward;
            EnemyState best = null;
            var bestDot = 0.3f; // roughly in front; nothing closer to her side counts as a target
            foreach (var e in _enemies.Where(e => e.IsAlive))
            {
                var d = (e.Position - origin).Flat;
                var dist = d.Magnitude;
                if (dist > BattleConfig.ShoveReach + e.BodyRadius) continue;
                var dot = dist > 0.0001f ? Vec3.Dot(facing, d.Normalized) : 1f;
                if (dot < bestDot) continue;
                bestDot = dot;
                best = e;
            }
            _shoveReadyAt = Now + BattleConfig.ShoveCooldownSeconds;
            Player.Root(BattleConfig.ShoveRootSeconds);
            Raise(BattleCue.Shove);
            if (best == null)
            {
                Raise(new BattleEvent(BattleCue.ShoveMissed) { At = origin });
                return;
            }
            best.Knockback(origin, BattleConfig.ShoveKnockbackMetres);
            best.ApplyStun(BattleConfig.ShoveStunSeconds);
            var dmg = Math.Max(1, (int)Math.Round(WeaponDamage() * BattleConfig.ShoveDamageFraction));
            DealDamage(best, dmg, critical: false, label: "SHOVE");
            Raise(BattleCue.ShoveLanded, best);
        }

        // ------------------------------------------------------------------ shootdodge (Max Payne dive)

        private bool CanShootdodge()
        {
            var w = State.Weapon;
            return w != null && !w.Melee && Player.CanAct && Player.ShootdodgeReady;
        }

        private void DoShootdodge()
        {
            if (!Player.TryStartShootdodge(Player.CurrentWish)) return;
            // The world slows to a crawl for as long as she's airborne; her own leap runs on real time.
            _bulletTime = true;
            Raise(BattleCue.ShootdodgeBegan);
        }

        /// <summary>One aimed round at the nearest target in weapon range, spent from her real clip.</summary>
        private void FireDuringShootdodge()
        {
            var w = State.Weapon;
            if (w == null || w.Melee || State.RoundsInClip <= 0)
            {
                Raise(BattleCue.Denied);
                return;
            }
            var origin = Player.Position;
            var target = _enemies.Where(e => e.IsAlive && (e.Position - origin).Flat.Magnitude <= w.Range + e.BodyRadius)
                .OrderBy(e => (e.Position - origin).Flat.Magnitude).FirstOrDefault();
            if (target == null)
            {
                Raise(BattleCue.Denied);
                return;
            }
            Player.FaceTowards(target.Position);
            Raise(BattleCue.Shot, target);
            var hit = DamageCalculator.Roll(WeaponDamage(), 1f, target.Stats.Defense, _rng);
            DealDamage(target, hit.Amount, hit.Critical);
            if (hit.Critical) target.Knockback(origin, 0.3f);
            State.SpendRounds(1);
            _lastAttackAt = Now;
            _lastAttackTarget = target;
        }

        /// <summary>Landing (shootdodging true → false) ends bullet time and costs HP; the body already rooted her.</summary>
        private void TrackShootdodgeLanding()
        {
            var now = Player.IsShootdodging;
            if (_wasShootdodging && !now)
            {
                _bulletTime = false;
                var dealt = State.TakeDamage(BattleConfig.ShootdodgeLandDamage);
                Raise(BattleCue.ShootdodgeLanded, amount: dealt);
            }
            _wasShootdodging = now;
        }

        // ------------------------------------------------------------------ damage

        private int WeaponDamage() => State.Weapon != null ? State.Weapon.Damage + State.Progression.Level : 8;

        private void DealDamage(EnemyState enemy, int amount, bool critical, string label = null)
        {
            var dealt = enemy.TakeHit(amount);
            Raise(new BattleEvent(BattleCue.EnemyDamaged) { Enemy = enemy, Amount = dealt, Flag = critical, Label = label });
        }

        private void OnEnemyDied(EnemyState enemy)
        {
            _expEarned += enemy.Stats.Exp;
            Raise(BattleCue.EnemyKilled, enemy);
        }

        /// <summary>Numbers that change whenever the battle's state does (replay comparison).</summary>
        public string Fingerprint()
        {
            var s = FormattableString.Invariant(
                $"{Stage} atb {_atb.Value:R} focus {_focus.IsOpen} exp {_expEarned} hp {State?.Health.Current} pe {State?.Pe.Current:R} rounds {State?.RoundsInClip}");
            return _enemies.Aggregate(s, (acc, e) => acc + " | " + e.Fingerprint());
        }
    }
}

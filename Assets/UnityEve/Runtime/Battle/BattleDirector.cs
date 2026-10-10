using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEve.Cameras;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using UnityEve.Data;
using UnityEve.Enemies;
using UnityEve.Game;
using UnityEve.Input;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Battle
{
    public enum BattlePhase
    {
        Inactive,
        /// <summary>Real time: the ATB fills, the player moves and dodges, enemies attack.</summary>
        Active,
        /// <summary>World paused: command menu (Attack / PE / Item / Equip / Escape).</summary>
        Command,
        /// <summary>World paused: range dome up, choosing a target.</summary>
        Targeting,
        /// <summary>Real time, player rooted: the chosen action plays out.</summary>
        Executing,
        Victory,
        Defeat,
    }

    /// <summary>
    /// Parasite Eve's Active Time Battle, plus the optional Focus layer.
    /// The world runs in real time while the ATB gauge fills; opening the command menu stops time;
    /// choosing Attack raises the range dome to pick a target; the action then plays out in real time.
    /// A perfectly-timed dodge opens a slow-motion Focus window with free bonus moves.
    /// </summary>
    public sealed partial class BattleDirector : MonoBehaviour
    {
        private static readonly object PauseKey = new object();
        private static readonly object FocusKey = new object();

        [SerializeField] private PlayerCharacter _player;
        [SerializeField] private RangeDome _dome;
        [SerializeField] private float _throwRange = 10f;

        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();
        private readonly List<EnemyActor> _targets = new List<EnemyActor>();
        private readonly AtbGauge _atb = new AtbGauge();
        private readonly FocusWindow _focus = new FocusWindow();
        private Rng _rng;
        private EncounterZone _zone;
        private MenuModel _menu;
        private readonly Stack<MenuModel> _menuStack = new Stack<MenuModel>();
        private int _targetIndex;
        private float _targetRange;
        private System.Action<EnemyActor> _onTargetChosen;
        private EnemyActor _focusAttacker;
        private int _expEarned;
        private bool _menuLocked;

        public static BattleDirector Instance { get; private set; }
        public BattlePhase Phase { get; private set; } = BattlePhase.Inactive;
        public bool InBattle => Phase != BattlePhase.Inactive;
        public AtbGauge Atb => _atb;
        public FocusWindow Focus => _focus;
        public IReadOnlyList<EnemyActor> Enemies => _enemies;
        public EncounterZone Zone => _zone;
        public MenuModel Menu => _menu;
        public int PerfectDodges => _focus.PerfectDodges;
        /// <summary>When true, encounter zones do not trigger (cutscenes, autopilot sightseeing).</summary>
        public bool SuppressEncounters { get; set; }

        public event System.Action<EncounterZone> BattleStarted;
        public event System.Action<EncounterZone, bool> BattleEnded;

        public void Configure(PlayerCharacter player, RangeDome dome)
        {
            _player = player;
            _dome = dome;
        }

        private void Awake()
        {
            Instance = this;
            _rng = new Rng((uint)System.Environment.TickCount);
            _atb.Filled += () =>
            {
                AudioService.Play(SoundId.AtbReady);
                GameInput.Instance?.Rumble(0.1f, 0.2f, 0.06f);
            };
            _focus.Opened += () => WorldTime.Instance?.SetScale(FocusKey, FocusWindow.SlowTimeScale);
            _focus.Closed += () =>
            {
                WorldTime.Instance?.SetScale(FocusKey, 1f);
                GameHud.Instance?.ShowFocus(-1f);
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ battle lifecycle

        public void Begin(EncounterZone zone)
        {
            if (InBattle || _player.IsDead) return;
            _zone = zone;
            _expEarned = 0;
            _enemies.Clear();
            _atb.Spend();
            _player.Pe.BeginBattle();
            NudgeInside(zone);

            var i = 0;
            foreach (var spawn in zone.Spawns)
            {
                if (spawn.Enemy == null || spawn.Enemy.Prefab == null) continue;
                var go = Instantiate(spawn.Enemy.Prefab, zone.SpawnWorldPosition(spawn), Quaternion.identity);
                go.transform.LookAt(new Vector3(_player.transform.position.x, go.transform.position.y, _player.transform.position.z));
                var actor = go.GetComponent<EnemyActor>();
                actor.Begin(_player.transform, MayCommit, 0.6f + _rng.Range(0f, 0.8f) + i * 0.25f);
                actor.Struck += OnEnemyStrike;
                actor.Died += OnEnemyDied;
                _enemies.Add(actor);
                i++;
            }

            zone.SealWalls(true);
            Phase = BattlePhase.Active;
            _player.Controller.Animator?.SetBool(AnimParams.InBattle, true);
            var hud = GameHud.Instance;
            hud?.SetBattleMode(true);
            hud?.ShowBanner("ENCOUNTER", zone.DisplayName, 1.2f);
            AudioService.Play(SoundId.EncounterStart);
            CameraDirector.Instance?.AddTrauma(0.25f);
            GameInput.Instance?.Rumble(0.4f, 0.6f, 0.2f);
            BattleStarted?.Invoke(zone);
        }

        private void NudgeInside(EncounterZone zone)
        {
            var b = zone.Bounds;
            var p = _player.transform.position;
            var inner = new Vector3(
                Mathf.Clamp(p.x, b.min.x + 0.8f, b.max.x - 0.8f), p.y,
                Mathf.Clamp(p.z, b.min.z + 0.8f, b.max.z - 0.8f));
            if ((inner - p).sqrMagnitude > 0.0001f) _player.Controller.Teleport(inner, _player.transform.rotation);
        }

        private bool MayCommit() => _enemies.Count(e => e.IsAlive && e.IsCommitted) < BattleConfig.MaxCommittedAttackers;

        private void End(bool won)
        {
            CloseMenus();
            _focus.Close();
            _dome.Hide();
            if (won) _zone.MarkCleared();
            else _zone.MarkEscaped();
            foreach (var e in _enemies)
                if (e != null && !e.Removed) Destroy(e.gameObject, won ? 3f : 0f);
            _enemies.Clear();
            _player.ClearBuffs();
            _player.Controller.Animator?.SetBool(AnimParams.InBattle, false);
            var hud = GameHud.Instance;
            hud?.SetBattleMode(false);
            hud?.ShowTarget(null, 0f);
            Phase = BattlePhase.Inactive;
            var zone = _zone;
            _zone = null;
            BattleEnded?.Invoke(zone, won);
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!InBattle) return;
            var input = GameInput.Instance;
            _focus.Tick(Time.unscaledDeltaTime);

            switch (Phase)
            {
                case BattlePhase.Active:
                    TickRealtime(Time.deltaTime);
                    if (_focus.IsOpen) HandleFocusInput(input);
                    else if (input != null && input.ConfirmPressed && _atb.IsFull && _player.Controller.CanAct) OpenCommandMenu();
                    break;
                case BattlePhase.Executing:
                    TickRealtime(Time.deltaTime);
                    break;
                case BattlePhase.Command:
                    HandleMenuInput(input);
                    break;
                case BattlePhase.Targeting:
                    HandleTargetInput(input);
                    break;
            }

            EnemyActor.Separate(_enemies, _player.transform);
            if (_focus.IsOpen) GameHud.Instance?.ShowFocus(_focus.Remaining01);
            CheckOutcome();
        }

        private void TickRealtime(float dt)
        {
            _atb.Tick(dt, _player.AtbRate, _player.Hasted);
            _player.Pe.Regenerate(dt);
        }

        private void CheckOutcome()
        {
            if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return;
            if (_player.IsDead)
            {
                Phase = BattlePhase.Defeat;
                CloseMenus();
                _focus.Close();
                StartCoroutine(DefeatRoutine());
                return;
            }
            if (_enemies.Count > 0 && _enemies.TrueForAll(e => !e.IsAlive) && Phase != BattlePhase.Executing)
            {
                Phase = BattlePhase.Victory;
                CloseMenus();
                _focus.Close();
                StartCoroutine(VictoryRoutine());
            }
        }

        private IEnumerator VictoryRoutine()
        {
            var hud = GameHud.Instance;
            AudioService.Play(SoundId.Victory);
            yield return new WaitForSeconds(0.6f);
            var levels = _player.GainExp(_expEarned);
            hud?.ShowBanner("VICTORY", $"EXP +{_expEarned}", 1.8f);
            if (levels > 0)
            {
                yield return new WaitForSeconds(0.9f);
                AudioService.Play(SoundId.LevelUp);
                hud?.ShowMessage($"Level up! LV {_player.Progression.Level}. Active Time rises; the gauge fills faster.", 3.5f);
            }
            yield return new WaitForSeconds(1.2f);
            End(won: true);
        }

        private IEnumerator DefeatRoutine()
        {
            _player.Controller.Animator?.SetTrigger(AnimParams.Die);
            yield return new WaitForSeconds(1.5f);
            var zone = _zone;
            End(won: false);
            GameSession.Instance?.OnPlayerDefeated(zone);
        }

        // ------------------------------------------------------------------ enemy strikes and Focus

        private void OnEnemyStrike(EnemyActor enemy)
        {
            if (!InBattle || !enemy.IsAlive) return;
            var d = enemy.transform.position - _player.transform.position;
            d.y = 0f;
            var outcome = EnemyAttackCycle.Resolve(d.magnitude, enemy.Stats.Reach, _player.Controller.IsInvulnerable);
            var hud = GameHud.Instance;
            switch (outcome)
            {
                case StrikeOutcome.Evaded:
                    if (FocusWindow.IsPerfect(_player.Controller.SecondsSinceDodge) && Phase == BattlePhase.Active)
                    {
                        _focusAttacker = enemy;
                        _focus.Open();
                        AudioService.Play(SoundId.PerfectDodge);
                        hud?.ShowFocus(1f);
                        GameInput.Instance?.Rumble(0.2f, 0.5f, 0.12f);
                    }
                    else
                    {
                        hud?.Popup(_player.transform.position + Vector3.up * 1.8f, "MISS", new Color(0.7f, 0.9f, 1f));
                    }
                    break;
                case StrikeOutcome.Hit:
                    var dealt = _player.TakeDamage(Mathf.RoundToInt(enemy.Stats.AttackDamage * _rng.Range(0.85f, 1.15f)));
                    var severity = Mathf.Min(1f, dealt / 14f);
                    hud?.Hurt(severity);
                    hud?.Popup(_player.transform.position + Vector3.up * 1.8f, dealt.ToString(), new Color(1f, 0.45f, 0.4f));
                    AudioService.Play(SoundId.PlayerHurt);
                    _player.Controller.Animator?.SetTrigger(AnimParams.Hit);
                    CameraDirector.Instance?.AddTrauma(0.25f + 0.3f * severity);
                    GameInput.Instance?.Rumble(0.9f, 1f, 0.18f);
                    break;
            }
        }

        private void HandleFocusInput(GameInput input)
        {
            if (input == null) return;
            if (input.FocusHealPressed) UseFocusMove(FocusMove.QuickHeal);
            else if (input.FocusVitalsPressed) UseFocusMove(FocusMove.TargetVitals);
            else if (input.FocusCounterPressed) UseFocusMove(FocusMove.Counter);
        }

        /// <summary>Spends the open Focus window on a free move. No ATB cost.</summary>
        public bool UseFocusMove(FocusMove move)
        {
            if (_focusAttacker == null || !_focus.TryUse(move)) return false;
            var hud = GameHud.Instance;
            switch (move)
            {
                case FocusMove.QuickHeal:
                    var healed = _player.Heal(FocusWindow.QuickHealAmount);
                    hud?.Popup(_player.transform.position + Vector3.up * 1.8f, $"+{healed}", new Color(0.5f, 1f, 0.6f));
                    BattleFx.Instance?.Aura(_player.transform, new Color(0.4f, 1f, 0.6f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case FocusMove.TargetVitals:
                    if (!_focusAttacker.IsAlive) break;
                    _player.Controller.FaceTowards(_focusAttacker.transform.position);
                    _player.Controller.Animator?.SetTrigger(AnimParams.Shoot);
                    var hit = DamageCalculator.Roll(WeaponDamage(), FocusWindow.VitalsMultiplier, _focusAttacker.Stats.Defense, _rng, canCrit: false);
                    ShotVisual(_focusAttacker);
                    DealDamage(_focusAttacker, hit.Amount, critical: true, label: "VITALS");
                    break;
                case FocusMove.Counter:
                    if (!_focusAttacker.IsAlive) break;
                    _focusAttacker.Knockback(_player.transform.position, 1.6f);
                    _focusAttacker.ApplyStun(FocusWindow.CounterStunSeconds);
                    _player.Controller.Animator?.SetTrigger(AnimParams.Throw);
                    DealDamage(_focusAttacker, Mathf.Max(1, WeaponDamage() / 2), false, "COUNTER");
                    break;
            }
            return true;
        }

        // ------------------------------------------------------------------ damage

        private int WeaponDamage() => _player.Weapon != null ? _player.Weapon.Stats.Damage + _player.Progression.Level : 8;

        private void DealDamage(EnemyActor enemy, int amount, bool critical, string label = null)
        {
            var dealt = enemy.TakeHit(amount);
            var text = label != null ? $"{label} {dealt}" : critical ? $"{dealt}!" : dealt.ToString();
            GameHud.Instance?.Popup(enemy.AimPoint, text, critical ? new Color(1f, 0.87f, 0.33f) : Color.white, critical);
            AudioService.Play(critical ? SoundId.HitCritical : SoundId.HitFlesh);
            BattleFx.Instance?.Burst(enemy.AimPoint, new Color(0.7f, 0.05f, 0.05f), critical ? 24 : 12, 3f);
            CameraDirector.Instance?.AddTrauma(critical ? 0.3f : 0.15f);
            GameInput.Instance?.Rumble(critical ? 0.6f : 0.35f, critical ? 1f : 0.75f, critical ? 0.16f : 0.07f);
            if (_targets.Count > 0 && Phase == BattlePhase.Targeting) RefreshTargetPanel();
            else GameHud.Instance?.ShowTarget($"{enemy.Stats.DisplayName}  LV{enemy.Stats.Level}", enemy.Health.Fraction);
        }

        private void OnEnemyDied(EnemyActor enemy) => _expEarned += enemy.Stats.Exp;

        private void ShotVisual(EnemyActor target)
        {
            var muzzle = _player.transform.position + Vector3.up * 1.25f + _player.transform.forward * 0.45f;
            BattleFx.Instance?.Tracer(muzzle, target.AimPoint, new Color(1f, 0.85f, 0.5f));
            AudioService.Play(_player.Weapon != null ? _player.Weapon.FireSound : SoundId.Gunshot);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Eve.Cameras;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Session;
using Unity.Eve.Core.Sim;
using Unity.Eve.Core.Util;
using Unity.Eve.Data;
using Unity.Eve.Enemies;
using Unity.Eve.Game;
using Unity.Eve.Input;
using Unity.Eve.Player;
using Unity.Eve.UI;

namespace Unity.Eve.Battle
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
        /// <summary>World paused: Line Rush's free-rotating aim line, not a target lock-on.</summary>
        Aiming,
        /// <summary>Real time, player rooted: the chosen action plays out.</summary>
        Executing,
        Victory,
        Defeat,
    }

    /// <summary>
    /// Parasite Eve's Active Time Battle, as the scene shows it. The battle itself (gauge, Focus, tokens, the
    /// creatures, every turn and roll) is the Core <see cref="BattleAuthority"/>, stepped at the fixed tick; this
    /// starts it when an encounter triggers, shows what it raises (popups, effects, sounds, the camera and
    /// rumble), and runs the paused command menu, range dome and aim line, submitting the finished choice to the
    /// session as a <see cref="BattleCommand"/>. Its public methods are the same front the self-play drives.
    /// </summary>
    public sealed partial class BattleDirector : MonoBehaviour
    {
        private static readonly object PauseKey = new object();
        private static readonly object FocusKey = new object();
        private static readonly object ShootdodgeKey = new object();

        [SerializeField] private PlayerCharacter _player;
        [SerializeField] private RangeDome _dome;
        [SerializeField] private float _throwRange = 10f;

        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();
        private readonly Dictionary<EnemyState, EnemyActor> _views = new Dictionary<EnemyState, EnemyActor>();
        private readonly Dictionary<EnemyDefinition, EnemyKind> _kinds = new Dictionary<EnemyDefinition, EnemyKind>();
        private readonly Dictionary<int, GameObject> _puddles = new Dictionary<int, GameObject>();
        private GameAuthority _authority;
        private Rng _seeds;
        private EncounterZone _zone;
        private GameObject _grenade;
        private bool _reloadShown;

        public static BattleDirector Instance { get; private set; }
        /// <summary>The battle's rules and state.</summary>
        public BattleAuthority Rules => _authority.Battle;
        public BattlePhase Phase
        {
            get
            {
                switch (Rules.Stage)
                {
                    case BattleStage.Active: return BattlePhase.Active;
                    case BattleStage.Command: return _menuPhase;
                    case BattleStage.Executing: return BattlePhase.Executing;
                    case BattleStage.Victory: return BattlePhase.Victory;
                    case BattleStage.Defeat: return BattlePhase.Defeat;
                    default: return BattlePhase.Inactive;
                }
            }
        }
        public bool InBattle => Rules.InBattle;
        public bool BossFight => Rules.BossFight;
        public AtbGauge Atb => Rules.Atb;
        public FocusWindow Focus => Rules.Focus;
        public IReadOnlyList<EnemyActor> Enemies => _enemies;
        public EncounterZone Zone => _zone;
        public PlayerCharacter Player => _player;
        public MenuModel Menu => _menu;
        public int PerfectDodges => Rules.PerfectDodges;
        /// <summary>The live active-reload minigame while one is running; null the rest of the time.</summary>
        public ActiveReload CurrentReload => Rules.CurrentReload;
        /// <summary>Seconds since Kat's last gun shot, Slash or Rush (for the backpedal window).</summary>
        public float SecondsSinceLastAttack => Rules.SecondsSinceLastAttack;
        /// <summary>Whoever she last attacked, alive or not; null before her first attack this battle.</summary>
        public EnemyActor LastAttackTarget => ViewOf(Rules.LastAttackTarget);
        /// <summary>When true, encounter zones do not trigger (cutscenes, autopilot sightseeing).</summary>
        public bool SuppressEncounters { get; set; }
        /// <summary>The replay log of the battle in progress, or of the last one: with its setup and seed it plays the
        /// battle again (a bug report that reproduces).</summary>
        public ReplayLog LastBattleLog { get; private set; }

        public event System.Action<EncounterZone> BattleStarted;
        public event System.Action<EncounterZone, bool> BattleEnded;
        /// <summary>Raised for every creature killed (kill count for the ending).</summary>
        public event System.Action<EnemyActor> EnemyKilled;

        public void Configure(PlayerCharacter player, RangeDome dome)
        {
            _player = player;
            _dome = dome;
        }

        private void Awake()
        {
            Instance = this;
            _authority = SimHost.For(gameObject.scene);
            // Each battle gets its own seed from this; the battle replays from that seed and its command log.
            _seeds = new Rng((uint)System.Environment.TickCount);
            Rules.EventRaised += OnBattleEvent;
        }

        private void OnDestroy()
        {
            if (_authority != null) Rules.EventRaised -= OnBattleEvent;
            if (Instance == this) Instance = null;
        }

        private EnemyActor ViewOf(EnemyState state) => state != null && _views.TryGetValue(state, out var view) ? view : null;

        /// <summary>Hands a finished choice to the session (the one command pipeline).</summary>
        private bool Submit(BattleOrder order, int target = -1, int index = -1, string item = null, float x = 0f, float z = 0f)
        {
            var command = new BattleCommand(_player.Seat, order, target, index, item, x, z);
            var session = GameSession.Instance != null ? GameSession.Instance.Session : null;
            return session != null ? session.Submit(command) : _authority.Handle(command) == null;
        }

        // ------------------------------------------------------------------ battle lifecycle

        public void Begin(EncounterZone zone)
        {
            if (InBattle || _player.IsDead) return;
            var b = zone.Bounds;
            var setup = new BattleSetup
            {
                Seed = _seeds.NextUInt(),
                Arena = new Arena(b.min.ToSim(), b.max.ToSim()),
                PackSize = zone.Spawns.Count(sp => sp.Enemy != null),
                ThrowRange = _throwRange,
            };
            foreach (var spawn in zone.Spawns)
            {
                if (spawn.Enemy == null || spawn.Enemy.Prefab == null) continue;
                var kind = KindOf(spawn.Enemy, 0);
                if (kind == null)
                {
                    Debug.LogError($"[Unity.Eve] enemy prefab for '{spawn.Enemy.name}' has no EnemyActor component; skipping spawn.");
                    continue;
                }
                setup.Spawns.Add(new EnemySpawn(kind, zone.SpawnWorldPosition(spawn).ToSim()));
            }

            _zone = zone;
            LastBattleLog = new ReplayLog();
            _authority.Recorder = LastBattleLog;
            if (!_authority.BeginBattle(_player.Seat, setup))
            {
                _authority.Recorder = null;
                _zone = null;
                return;
            }

            zone.SealWalls(true);
            _player.Controller.Animator?.SetBool(AnimParams.InBattle, true);
            var hud = GameHud.Instance;
            hud?.SetBattleMode(true);
            hud?.ShowBanner("ENCOUNTER", zone.DisplayName, 1.2f);
            AudioService.Play(SoundId.EncounterStart);
            CameraDirector.Instance?.AddTrauma(0.25f);
            GameInput.Instance?.Rumble(0.4f, 0.6f, 0.2f);
            BattleStarted?.Invoke(zone);
        }

        /// <summary>A definition as the authority sees it (its prefab's body measurements; its brood).</summary>
        private EnemyKind KindOf(EnemyDefinition definition, int depth)
        {
            if (definition == null || definition.Prefab == null || depth > 4) return null;
            if (_kinds.TryGetValue(definition, out var kind)) return kind;
            var actor = definition.Prefab.GetComponent<EnemyActor>();
            if (actor == null) return null;
            kind = new EnemyKind(definition.Stats, actor.BodyRadius, actor.AimHeight, KindOf(definition.Brood, depth + 1), definition);
            _kinds[definition] = kind;
            return kind;
        }

        private void ShowSpawn(EnemyState state)
        {
            var definition = (EnemyDefinition)state.Kind.Token;
            var go = Instantiate(definition.Prefab, state.Position.ToUnity(), SimConvert.YawRotation(state.Yaw));
            var actor = go.GetComponent<EnemyActor>();
            actor.Bind(state, _authority.Clock, _player.transform);
            _enemies.Add(actor);
            _views[state] = actor;
        }

        private void ShowEnd(bool won, bool defeated)
        {
            CloseMenus();
            WorldTime.Instance?.SetScale(ShootdodgeKey, 1f);
            WorldTime.Instance?.SetScale(FocusKey, 1f);
            _dome.Hide();
            if (_zone != null)
            {
                if (won) _zone.MarkCleared();
                else _zone.MarkEscaped();
            }
            foreach (var e in _enemies)
                if (e != null && !e.Removed) Destroy(e.gameObject, won ? 3f : 0f);
            _enemies.Clear();
            _views.Clear();
            _targets.Clear();
            if (_grenade != null) Destroy(_grenade);
            _player.Controller.Animator?.SetBool(AnimParams.InBattle, false);
            var hud = GameHud.Instance;
            hud?.SetBattleMode(false);
            hud?.ShowTarget(null, 0f);
            if (_reloadShown) hud?.ShowReload(-1f, 0f, 0f);
            _reloadShown = false;
            if (_authority.Recorder == LastBattleLog) _authority.Recorder = null;
            var zone = _zone;
            _zone = null;
            BattleEnded?.Invoke(zone, won);
            if (defeated) GameSession.Instance?.OnPlayerDefeated();
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!InBattle) return;
            if (Rules.Stage == BattleStage.Command)
            {
                var input = _player.Controls;
                switch (_menuPhase)
                {
                    case BattlePhase.Command:
                        HandleMenuInput(input);
                        break;
                    case BattlePhase.Targeting:
                        HandleTargetInput(input);
                        break;
                    case BattlePhase.Aiming:
                        HandleAimInput(input);
                        break;
                }
            }

            var hud = GameHud.Instance;
            if (Rules.Focus.IsOpen) hud?.ShowFocus(Rules.Focus.Remaining01);
            var reload = Rules.CurrentReload;
            if (reload != null)
            {
                hud?.ShowReload(reload.Progress01, reload.SweetSpotStart01, reload.SweetSpotEnd01);
                _reloadShown = true;
            }
            else if (_reloadShown)
            {
                hud?.ShowReload(-1f, 0f, 0f);
                _reloadShown = false;
            }
        }

        // ------------------------------------------------------------------ what the battle raises

        private void OnBattleEvent(BattleEvent e)
        {
            var hud = GameHud.Instance;
            var fx = BattleFx.Instance;
            var cam = CameraDirector.Instance;
            var pad = GameInput.Instance;
            var me = _player.transform;
            var enemy = ViewOf(e.Enemy);
            switch (e.Cue)
            {
                case BattleCue.EnemySpawned:
                    ShowSpawn(e.Enemy);
                    break;
                case BattleCue.BroodCalled:
                    hud?.ShowBanner("THE MOTHER SCREAMS", "Her brood claws up through the floor.", 1.6f);
                    AudioService.Play(SoundId.EnemyBuff);
                    cam?.AddTrauma(0.5f);
                    pad?.Rumble(0.8f, 1f, 0.4f);
                    break;
                case BattleCue.AtbFilled:
                    AudioService.Play(SoundId.AtbReady);
                    pad?.Rumble(0.1f, 0.2f, 0.06f);
                    break;
                case BattleCue.MenuOpened:
                    WorldTime.Instance?.Pause(PauseKey);
                    _menuPhase = BattlePhase.Command;
                    _menu = null;
                    _menuStack.Clear();
                    PushMenu(BuildCommandMenu());
                    break;
                case BattleCue.MenuClosed:
                    CloseMenus();
                    break;
                case BattleCue.FocusOpened:
                    WorldTime.Instance?.SetScale(FocusKey, FocusWindow.SlowTimeScale);
                    break;
                case BattleCue.FocusClosed:
                    WorldTime.Instance?.SetScale(FocusKey, 1f);
                    hud?.ShowFocus(-1f);
                    break;
                case BattleCue.PerfectDodge:
                    AudioService.Play(SoundId.PerfectDodge);
                    hud?.ShowFocus(1f);
                    if (!e.Flag) pad?.Rumble(0.2f, 0.5f, 0.12f);
                    break;
                case BattleCue.StrikeEvaded:
                    hud?.Popup(me.position + Vector3.up * 1.8f, "MISS", new Color(0.7f, 0.9f, 1f));
                    break;
                case BattleCue.PlayerHit:
                {
                    var severity = Mathf.Min(1f, e.Amount / 14f);
                    hud?.Hurt(severity);
                    hud?.Popup(me.position + Vector3.up * 1.8f, e.Amount.ToString(), new Color(1f, 0.45f, 0.4f));
                    if (e.Flag)
                    {
                        hud?.Popup(me.position + Vector3.up * 2.3f, "COVER", new Color(0.6f, 0.85f, 1f));
                        AudioService.Play(SoundId.CoverBlock);
                    }
                    else
                    {
                        AudioService.Play(SoundId.PlayerHurt);
                    }
                    _player.Controller.Animator?.SetTrigger(AnimParams.Hit);
                    cam?.AddTrauma(0.25f + 0.3f * severity);
                    pad?.Rumble(0.9f, 1f, 0.18f);
                    break;
                }
                case BattleCue.PuddleSpawned:
                    var disc = HazardPuddle.Show(e.At.ToUnity());
                    if (disc != null) _puddles[e.Index] = disc;
                    break;
                case BattleCue.PuddleBurned:
                    hud?.Hurt(0.15f);
                    hud?.Popup(me.position + Vector3.up * 1.6f, e.Amount.ToString(), new Color(0.6f, 1f, 0.3f));
                    AudioService.Play(SoundId.AcidSizzle, 0.5f);
                    break;
                case BattleCue.PuddleGone:
                    if (_puddles.TryGetValue(e.Index, out var gone))
                    {
                        if (gone != null) Destroy(gone);
                        _puddles.Remove(e.Index);
                    }
                    break;
                case BattleCue.FocusHealed:
                    hud?.Popup(me.position + Vector3.up * 1.8f, $"+{e.Amount}", new Color(0.5f, 1f, 0.6f));
                    fx?.Aura(me, new Color(0.4f, 1f, 0.6f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case BattleCue.Motion:
                    _player.Controller.Animator?.SetTrigger(MotionTrigger(e.Motion));
                    break;
                case BattleCue.Shot:
                    if (enemy != null) ShotVisual(enemy);
                    break;
                case BattleCue.EnemyDamaged:
                    if (enemy != null) ShowDamage(enemy, e.Amount, e.Flag, e.Label);
                    break;
                case BattleCue.EnemyKilled:
                    if (enemy != null) EnemyKilled?.Invoke(enemy);
                    break;
                case BattleCue.ShotMissed:
                    if (enemy != null) hud?.Popup(enemy.AimPoint, "MISS", new Color(0.7f, 0.7f, 0.7f));
                    break;
                case BattleCue.Shove:
                    _player.Controller.Animator?.SetTrigger(AnimParams.Shove);
                    AudioService.Play(SoundId.Shove);
                    break;
                case BattleCue.ShoveMissed:
                    hud?.Popup(e.At.ToUnity() + Vector3.up * 1.8f, "MISS", new Color(0.7f, 0.9f, 1f));
                    break;
                case BattleCue.ShoveLanded:
                    cam?.AddTrauma(0.15f);
                    pad?.Rumble(0.3f, 0.5f, 0.1f);
                    break;
                case BattleCue.ShootdodgeBegan:
                    // The world (enemy windups, their strikes) slows to a crawl while she's airborne.
                    WorldTime.Instance?.SetScale(ShootdodgeKey, BattleConfig.ShootdodgeTimeScale);
                    break;
                case BattleCue.ShootdodgeLanded:
                    WorldTime.Instance?.SetScale(ShootdodgeKey, 1f);
                    hud?.Hurt(0.12f);
                    hud?.Popup(me.position + Vector3.up * 1.6f, e.Amount > 0 ? $"-{e.Amount}" : "LANDED", new Color(1f, 0.7f, 0.3f));
                    cam?.AddTrauma(0.3f);
                    break;
                case BattleCue.Denied:
                    AudioService.Play(SoundId.UiDeny);
                    break;
                case BattleCue.KatanaDrawn:
                    _player.Katana?.Draw();
                    _player.Katana?.Trail(true);
                    break;
                case BattleCue.KatanaSheathed:
                    _player.Katana?.Sheathe();
                    break;
                case BattleCue.BladeHit:
                    if (enemy != null) fx?.Burst(enemy.AimPoint, BladeColor, e.Flag ? 26 : 14, 5f, 0.05f);
                    break;
                case BattleCue.SlashSwing:
                    _player.Controller.Animator?.SetTrigger(AnimParams.Slash);
                    AudioService.Play(SoundId.KatanaSlash);
                    break;
                case BattleCue.SlashLanded:
                    cam?.AddTrauma(0.2f);
                    break;
                case BattleCue.RushBegan:
                    _player.Controller.Animator?.SetTrigger(AnimParams.Rush);
                    AudioService.Play(SoundId.KatanaRush);
                    fx?.Tracer(e.At.ToUnity() + Vector3.up * 1.0f, e.To.ToUnity() + Vector3.up * 1.0f, BladeColor);
                    break;
                case BattleCue.RushEnded:
                {
                    var end = e.To.ToUnity();
                    _player.Controller.Animator?.SetTrigger(AnimParams.Slash);
                    fx?.Flash(end + Vector3.up, BladeColor, 6f, 5f, 0.25f);
                    cam?.AddTrauma(0.35f);
                    pad?.Rumble(0.5f, 0.9f, 0.18f);
                    if (e.Amount > 1) hud?.Popup(end + Vector3.up * 2.2f, $"{e.Amount} CUT!", new Color(1f, 0.8f, 0.25f), big: true);
                    break;
                }
                case BattleCue.Cast:
                    _player.Controller.Animator?.SetTrigger(AnimParams.Cast);
                    AudioService.Play(SoundId.PeCast);
                    if (e.Index >= 0 && e.Index < _player.Abilities.Length) fx?.Aura(me, _player.Abilities[e.Index].Tint);
                    break;
                case BattleCue.AbilityHealed:
                    hud?.Popup(me.position + Vector3.up * 1.8f, $"+{e.Amount}", new Color(0.5f, 1f, 0.6f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case BattleCue.EnergyShot:
                    if (enemy != null && e.Index >= 0 && e.Index < _player.Abilities.Length)
                        fx?.Tracer(me.position + Vector3.up * 1.2f, enemy.AimPoint, _player.Abilities[e.Index].Tint);
                    break;
                case BattleCue.Hasted:
                    hud?.ShowMessage($"Haste: the gauge fills twice as fast for {e.Value:0}s.");
                    break;
                case BattleCue.Barriered:
                    hud?.ShowMessage($"Barrier: damage halved for {e.Value:0}s.");
                    break;
                case BattleCue.Slowed:
                    hud?.ShowMessage($"Slow: the creatures move at half speed for {e.Value:0}s.");
                    break;
                case BattleCue.ItemHealed:
                    hud?.Popup(me.position + Vector3.up * 1.8f, $"+{e.Amount}", new Color(0.5f, 1f, 0.6f));
                    fx?.Aura(me, new Color(0.4f, 1f, 0.6f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case BattleCue.PeRestored:
                    fx?.Aura(me, new Color(0.4f, 0.6f, 1f));
                    AudioService.Play(SoundId.Heal);
                    break;
                case BattleCue.ThrowStarted:
                    _player.Controller.Animator?.SetTrigger(AnimParams.Throw);
                    AudioService.Play(SoundId.Throw);
                    break;
                case BattleCue.Thrown:
                    StartCoroutine(FlyGrenade(e.At.ToUnity(), e.To.ToUnity(), e.Value));
                    break;
                case BattleCue.Burst:
                {
                    if (_grenade != null) Destroy(_grenade);
                    _grenade = null;
                    var end = e.To.ToUnity();
                    if (e.Flag)
                    {
                        fx?.Explosion(end, e.Value, new Color(1f, 0.55f, 0.15f));
                        AudioService.Play(SoundId.Explosion);
                        cam?.AddTrauma(0.6f);
                        pad?.Rumble(1f, 1f, 0.35f);
                    }
                    else
                    {
                        fx?.Flash(end + Vector3.up, Color.white, 12f, e.Value * 3f, 0.4f);
                        fx?.Burst(end, Color.white, 40, 6f);
                        AudioService.Play(SoundId.Explosion, 0.5f);
                    }
                    break;
                }
                case BattleCue.Reloaded:
                    if (e.Flag)
                    {
                        AudioService.Play(SoundId.ActiveReload);
                        hud?.ShowMessage("Active reload!");
                        hud?.Popup(me.position + Vector3.up * 1.8f, "ACTIVE RELOAD", new Color(1f, 0.87f, 0.33f));
                    }
                    else
                    {
                        AudioService.Play(SoundId.Reload);
                        hud?.ShowMessage("Reloaded.");
                    }
                    break;
                case BattleCue.Escaped:
                    hud?.ShowMessage("Kat slips away.");
                    AudioService.Play(SoundId.Dodge);
                    break;
                case BattleCue.EscapeFailed:
                    hud?.ShowMessage("Couldn't escape!");
                    AudioService.Play(SoundId.UiDeny);
                    break;
                case BattleCue.VictoryBegan:
                    AudioService.Play(SoundId.Victory);
                    break;
                case BattleCue.Victory:
                    hud?.ShowBanner("VICTORY", $"EXP +{e.Amount}", 1.8f);
                    break;
                case BattleCue.LevelUp:
                    AudioService.Play(SoundId.LevelUp);
                    hud?.ShowMessage($"Level up! LV {e.Amount}. Active Time rises; the gauge fills faster.", 3.5f);
                    break;
                case BattleCue.Ended:
                    ShowEnd(e.Flag, e.Index == 1);
                    break;
            }
        }

        private static int MotionTrigger(PlayerMotion motion)
        {
            switch (motion)
            {
                case PlayerMotion.Shoot: return AnimParams.Shoot;
                case PlayerMotion.Slash: return AnimParams.Slash;
                case PlayerMotion.Rush: return AnimParams.Rush;
                case PlayerMotion.Cast: return AnimParams.Cast;
                case PlayerMotion.Throw: return AnimParams.Throw;
                case PlayerMotion.Shove: return AnimParams.Shove;
                default: return AnimParams.Die;
            }
        }

        /// <summary>A visible grenade on a ballistic arc, on scaled time (it freezes if the world pauses); the
        /// authority decides when it lands.</summary>
        private IEnumerator FlyGrenade(Vector3 start, Vector3 end, float flight)
        {
            if (_grenade != null) Destroy(_grenade);
            var grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(grenade.GetComponent<Collider>());
            grenade.transform.localScale = Vector3.one * 0.14f;
            _grenade = grenade;
            for (var t = 0f; grenade != null && t < flight; t += Time.deltaTime)
            {
                var k = t / flight;
                grenade.transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 2f);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ Focus (the same front the self-play drives)

        /// <summary>Spends the open Focus window on a free move. No ATB cost.</summary>
        public bool UseFocusMove(FocusMove move) => Submit(BattleOrder.FocusMove, index: (int)move);

        /// <summary>Opens a Focus window against the nearest enemy, as if a perfect dodge had happened.</summary>
        public bool DebugOpenFocus() => Phase == BattlePhase.Active && Submit(BattleOrder.DebugFocus);

        // ------------------------------------------------------------------ damage

        private void ShowDamage(EnemyActor enemy, int dealt, bool critical, string label)
        {
            var text = label != null ? $"{label} {dealt}" : critical ? $"{dealt}!" : dealt.ToString();
            GameHud.Instance?.Popup(enemy.AimPoint, text, critical ? new Color(1f, 0.87f, 0.33f) : Color.white, critical);
            AudioService.Play(critical ? SoundId.HitCritical : SoundId.HitFlesh);
            BattleFx.Instance?.Burst(enemy.AimPoint, new Color(0.7f, 0.05f, 0.05f), critical ? 24 : 12, 3f);
            CameraDirector.Instance?.AddTrauma(critical ? 0.3f : 0.15f);
            GameInput.Instance?.Rumble(critical ? 0.6f : 0.35f, critical ? 1f : 0.75f, critical ? 0.16f : 0.07f);
            if (_targets.Count > 0 && Phase == BattlePhase.Targeting) RefreshTargetPanel();
            else GameHud.Instance?.ShowTarget($"{enemy.Stats.DisplayName}  LV{enemy.Stats.Level}", enemy.Health.Fraction);
        }

        private void ShotVisual(EnemyActor target)
        {
            var muzzle = _player.transform.position + Vector3.up * 1.25f + _player.transform.forward * 0.45f;
            BattleFx.Instance?.Tracer(muzzle, target.AimPoint, new Color(1f, 0.85f, 0.5f));
            AudioService.Play(_player.Weapon != null ? _player.Weapon.FireSound : SoundId.Gunshot);
        }
    }
}

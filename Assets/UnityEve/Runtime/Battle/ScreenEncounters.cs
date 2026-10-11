using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEve.Cameras;
using UnityEve.Core.Battle;
using UnityEve.Core.Util;
using UnityEve.Data;

namespace UnityEve.Battle
{
    [Serializable]
    public sealed class RoamingPack
    {
        public EnemyDefinition[] Enemies = Array.Empty<EnemyDefinition>();
    }

    /// <summary>A screen that can host random encounters: its camera zone, its arena and what roams there.</summary>
    [Serializable]
    public sealed class RoamingScreen
    {
        public CameraZone Screen;
        public EncounterZone Arena;
        public List<RoamingPack> Packs = new List<RoamingPack>();
    }

    /// <summary>
    /// Random encounters on returning to a screen. Feeds every camera-zone change to the Core
    /// <see cref="ScreenEncounterRule"/> (neighbouring zones, found by overlapping volumes, are adjacent)
    /// and, when it says so, materializes a pack from that screen's table around the player in the
    /// screen's arena. Presentation only: the rule decides when.
    /// </summary>
    public sealed class ScreenEncounters : MonoBehaviour
    {
        [SerializeField] private List<RoamingScreen> _screens = new List<RoamingScreen>();
        [SerializeField] private uint _seed = 20261011;

        private ScreenEncounterRule _rule;
        private Rng _rng;
        private readonly Dictionary<CameraZone, int> _ids = new Dictionary<CameraZone, int>();

        public static ScreenEncounters Instance { get; private set; }
        /// <summary>Off while the autopilot does scripted steps; on for players.</summary>
        public bool Enabled { get; set; } = true;
        public ScreenEncounterRule Rule => _rule;
        public IReadOnlyList<RoamingScreen> Screens => _screens;

        public void Configure(List<RoamingScreen> screens) => _screens = screens;

        private void Awake()
        {
            Instance = this;
            _rng = new Rng(_seed ^ (uint)Environment.TickCount);
            _rule = new ScreenEncounterRule(new Rng(_rng.NextUInt()), initialCooldownElapsed: BattleConfig.ScreenEncounterCooldownSeconds);

            // Every camera zone is a screen; overlapping (or touching) volumes are neighbours.
            var zones = FindObjectsByType<CameraZone>(FindObjectsSortMode.InstanceID).ToList();
            for (var i = 0; i < zones.Count; i++) _ids[zones[i]] = i;
            for (var i = 0; i < zones.Count; i++)
            {
                var a = zones[i].GetComponent<BoxCollider>().bounds;
                a.Expand(0.6f);
                for (var j = i + 1; j < zones.Count; j++)
                    if (a.Intersects(zones[j].GetComponent<BoxCollider>().bounds)) _rule.Connect(i, j);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (CameraDirector.Instance != null) CameraDirector.Instance.ZoneChanged += OnZoneChanged;
            if (BattleDirector.Instance != null) BattleDirector.Instance.BattleEnded += (_, __) => _rule.BattleEnded();
        }

        private void Update() => _rule.Tick(Time.deltaTime);

        private void OnZoneChanged(CameraZone zone)
        {
            if (zone == null || !_ids.TryGetValue(zone, out var id)) return;
            var battle = BattleDirector.Instance;
            var screen = _screens.FirstOrDefault(s => s.Screen == zone && s.Arena != null && s.Packs.Count > 0);
            var busy = battle == null || battle.InBattle || battle.SuppressEncounters || !Enabled;
            // Visits always count, even while busy, so the "went somewhere far" memory stays true.
            if (!_rule.Enter(id, canHost: screen != null && !busy)) return;
            Begin(screen);
        }

        private void Begin(RoamingScreen screen)
        {
            var battle = BattleDirector.Instance;
            var player = battle.Player.transform.position;
            var pack = screen.Packs[_rng.Range(0, screen.Packs.Count)];
            var arena = screen.Arena;
            var b = arena.Bounds;
            var spawns = new List<EncounterSpawn>();
            var start = _rng.Range(0f, Mathf.PI * 2f);
            for (var i = 0; i < pack.Enemies.Length; i++)
            {
                // A loose ring around her, kept inside the arena.
                var angle = start + i * (Mathf.PI * 2f / pack.Enemies.Length) + _rng.Range(-0.3f, 0.3f);
                var r = _rng.Range(3.5f, 5.5f);
                var world = player + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                world.x = Mathf.Clamp(world.x, b.min.x + 1f, b.max.x - 1f);
                world.z = Mathf.Clamp(world.z, b.min.z + 1f, b.max.z - 1f);
                world.y = player.y;
                spawns.Add(new EncounterSpawn { Enemy = pack.Enemies[i], LocalPosition = arena.transform.InverseTransformPoint(world) });
            }
            arena.SetSpawns(spawns);
            battle.Begin(arena);
        }
    }
}

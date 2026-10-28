using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Eve.Core.Battle;
using Unity.Eve.Core.Session;
using Unity.Eve.Core.Sim;

namespace Unity.Eve.Core.Tests
{
    /// <summary>
    /// The authority and its fixed tick (U.EVE-LAW-6): a battle played through the session from one seat's
    /// input and battle orders replays from its seed plus the recorded log, to the same events and the same state.
    /// </summary>
    public class AuthorityReplayTests
    {
        /// <summary>A flat floor at y = 0 with nothing on it: the body's only physics in these tests.</summary>
        private sealed class FlatFloor : IBodyPhysics
        {
            public FlatFloor(Vec3 start) => Position = start;
            public Vec3 Position { get; private set; }
            public bool IsGrounded { get; private set; } = true;
            public float Yaw { get; private set; }

            public void Move(Vec3 motion)
            {
                var p = Position + motion;
                IsGrounded = p.Y <= 0f;
                Position = IsGrounded ? p.WithY(0f) : p;
            }

            public void Teleport(Vec3 position) => Position = position;
            public void SetYaw(float yaw) => Yaw = yaw;

            public bool TryObstacleAhead(Vec3 from, Vec3 direction, out float distance)
            {
                distance = 0f;
                return false;
            }

            public float ObstacleRadius => 1f;
        }

        private static readonly WeaponStats Pistol = new WeaponStats { Id = "pistol", Damage = 12, Range = 7f, ClipSize = 6, RootSeconds = 0.45f };
        private static readonly WeaponStats Smg = new WeaponStats { Id = "smg", Damage = 9, Range = 6f, ClipSize = 20, RoundsPerTurn = 3, BurstFireRounds = 6, BurstFireAccuracy = 0.6f };

        private static readonly Dictionary<string, ItemStats> Items = new Dictionary<string, ItemStats>
        {
            ["ammo"] = new ItemStats { Id = "ammo", Effect = ItemEffect.Ammo, MaxStack = 9, UsableInBattle = false },
            ["medicine"] = new ItemStats { Id = "medicine", Effect = ItemEffect.RestoreHp, Power = 40 },
            ["grenade"] = new ItemStats { Id = "grenade", Effect = ItemEffect.Explosive, Power = 30, Radius = 2.5f },
        };

        private static readonly AbilityStats[] Abilities =
        {
            new AbilityStats { Id = "shot", Effect = AbilityEffect.EnergyShot, PeCost = 10f, Power = 20, Range = 8f },
            new AbilityStats { Id = "haste", Effect = AbilityEffect.Haste, PeCost = 15f, DurationSeconds = 6f },
        };

        private static readonly EnemyKind Spitter = new EnemyKind(
            new EnemyStats { Id = "spitter", MaxHp = 40, Reach = 4f, Ranged = true, LeavesPuddle = true, Aggression = 0.7f, Exp = 12 }, 0.4f, 0.4f);
        private static readonly EnemyKind Rat = new EnemyKind(new EnemyStats { Id = "rat", MaxHp = 35, Aggression = 0.8f }, 0.35f, 0.3f);
        private static readonly EnemyKind Brute = new EnemyKind(new EnemyStats { Id = "brute", MaxHp = 70, Reach = 1.4f, Caution = 0.6f, Exp = 25 }, 0.5f, 0.6f);

        private static BattleSetup Setup(uint seed)
        {
            var setup = new BattleSetup { Seed = seed, Arena = new Arena(new Vec3(-9f, -1f, -9f), new Vec3(9f, 3f, 9f)), PackSize = 4 };
            setup.Spawns.Add(new EnemySpawn(Rat, new Vec3(0f, 0f, 4f)));
            setup.Spawns.Add(new EnemySpawn(Spitter, new Vec3(4f, 0f, 3f)));
            setup.Spawns.Add(new EnemySpawn(Rat, new Vec3(-3.5f, 0f, 4.5f)));
            setup.Spawns.Add(new EnemySpawn(Brute, new Vec3(1f, 0f, -5f)));
            return setup;
        }

        private sealed class World
        {
            public readonly GameAuthority Authority = new GameAuthority();
            public readonly PlayerState State;
            public readonly PlayerBody Body;
            public readonly List<string> Trace = new List<string>();

            public World()
            {
                State = new PlayerState(PlayerId.Of(0), new[] { new WeaponSlot(Pistol, "ammo"), new WeaponSlot(Smg, "ammo") }, 0, Abilities,
                    12, id => Items.TryGetValue(id, out var s) ? s.MaxStack : 9, id => Items.TryGetValue(id, out var s) ? s : null);
                State.Bag.Add("ammo", 4);
                State.Bag.Add("medicine", 2);
                State.Bag.Add("grenade", 2);
                Body = new PlayerBody(State, new FlatFloor(new Vec3(0.5f, 0f, -0.5f)), Authority.Clock);
                Authority.Register(Body);
                Authority.Battle.EventRaised += e => Trace.Add($"{Authority.Clock.Tick} {e}");
                Body.DodgeStarted += () => Trace.Add($"{Authority.Clock.Tick} dodge");
                Body.ShootdodgeStarted += () => Trace.Add($"{Authority.Clock.Tick} leap");
                Body.ShootdodgeLanded += () => Trace.Add($"{Authority.Clock.Tick} land");
            }

            public string Fingerprint() =>
                $"tick {Authority.Clock.Tick} now {Authority.Clock.NowExact:R}\n{Body.Fingerprint()}\n{Authority.Battle.Fingerprint()}\n" +
                $"bag {string.Join(",", State.Bag.Slots.Select(s => s.ItemId + "x" + s.Count))} weapon {State.WeaponIndex} rounds {State.RoundsInClip} lv {State.Progression.Level}";
        }

        /// <summary>
        /// Plays one seat through the live path: an intent per frame into the session (presses held over to the
        /// tick), battle orders through the session, the authority stepping one fixed tick per frame at the time
        /// scale the battle asks for, with every tick and command recorded.
        /// </summary>
        private static (World world, ReplayLog log) Play(uint seed, int ticks)
        {
            var w = new World();
            var session = new LocalSession(players: 1) { Handler = w.Authority };
            w.Authority.Input = seat => session.ControlsOf(seat);
            var log = new ReplayLog();
            w.Authority.Recorder = log;
            var seat = session.LocalPlayer;
            var battle = w.Authority.Battle;
            Assert.That(w.Authority.BeginBattle(seat, Setup(seed)), Is.True);

            var turn = 0;
            for (var frame = 1; frame <= ticks; frame++)
            {
                // Battle orders, from the paused menu.
                if (battle.Stage == BattleStage.Command)
                {
                    var nearest = battle.NearestLivingEnemy(w.Body.Position);
                    var target = nearest != null ? nearest.Id : -1;
                    var order = (turn++ % 7) switch
                    {
                        0 => new BattleCommand(seat, BattleOrder.Attack, target),
                        1 => new BattleCommand(seat, BattleOrder.Slash, target),
                        2 => new BattleCommand(seat, BattleOrder.Item, target, item: "grenade"),
                        3 => new BattleCommand(seat, BattleOrder.Ability, target, index: 0),
                        4 => new BattleCommand(seat, BattleOrder.Rush, target),
                        5 => new BattleCommand(seat, BattleOrder.Equip, index: 1),
                        _ => new BattleCommand(seat, w.State.NeedsReload ? BattleOrder.Reload : BattleOrder.BurstFire, target),
                    };
                    if (!session.Submit(order) || battle.Stage == BattleStage.Command)
                        if (!session.Submit(new BattleCommand(seat, BattleOrder.Attack, target)))
                            session.Submit(new BattleCommand(seat, BattleOrder.CancelMenu));
                }
                if (frame == 300) session.Submit(new TeleportCommand(seat, 1f, 0f, 1f, 0.5f));
                if (frame == 900 && battle.Stage == BattleStage.Active) session.Submit(new BattleCommand(seat, BattleOrder.DebugFocus));

                // The stick and buttons of this frame.
                var buttons = IntentButtons.None;
                if (battle.Stage == BattleStage.Active && battle.Atb.IsFull && frame % 3 == 0) buttons |= IntentButtons.Confirm;
                if (frame % 97 == 0) buttons |= IntentButtons.Cancel;
                if (frame % 331 == 0) buttons |= IntentButtons.Interact;
                if (frame % 613 == 0) buttons |= IntentButtons.Shootdodge;
                if (w.Body.IsShootdodging && frame % 5 == 0) buttons |= IntentButtons.Confirm;
                if (battle.Focus.IsOpen) buttons |= IntentButtons.FocusVitals;
                var threat = battle.NearestLivingEnemy(w.Body.Position);
                float mx = 0f, my = 0f;
                if (threat != null && frame % 240 < 180)
                {
                    var to = (threat.Position - w.Body.Position).Flat;
                    var away = to.Magnitude < 3f ? -1f : 1f;
                    var d = to.Normalized * away;
                    mx = d.X;
                    my = d.Z;
                }
                session.Submit(new IntentCommand(seat, frame, mx, my, 0, 0, buttons, cameraYaw: 0.3f * (frame / 600)));
                w.Authority.Advance(SimClock.TickSeconds, () => battle.SuggestedTimeScale);
            }
            return (w, log);
        }

        private static World Replay(ReplayLog log)
        {
            var w = new World();
            var text = log.Lines.ToArray(); // the log as it would be stored: lines of text
            ReplayLog.Replay(w.Authority, text, Setup);
            return w;
        }

        [Test]
        public void A_battle_replays_from_its_seed_and_command_log_to_the_same_events_and_state()
        {
            var (live, log) = Play(seed: 20261028u, ticks: 2400);

            // The run exercised the authority, not an idle screen.
            Assert.That(log.Lines.Count(l => l.StartsWith("cmd|")), Is.GreaterThan(5), "battle orders were applied");
            Assert.That(live.Trace.Count(t => t.Contains(nameof(BattleCue.EnemyDamaged))), Is.GreaterThan(3), "creatures were hurt");
            Assert.That(live.Trace.Any(t => t.EndsWith(" dodge")), Is.True, "she dodged");
            Assert.That(live.Trace.Any(t => t.Contains(nameof(BattleCue.PlayerHit)) || t.Contains(nameof(BattleCue.StrikeEvaded))), Is.True, "creatures struck at her");

            var replayed = Replay(log);
            Assert.That(replayed.Trace, Is.EqualTo(live.Trace));
            Assert.That(replayed.Fingerprint(), Is.EqualTo(live.Fingerprint()));
        }

        [Test]
        public void Replaying_twice_gives_the_same_run()
        {
            var (_, log) = Play(seed: 7u, ticks: 1500);
            var a = Replay(log);
            var b = Replay(log);
            Assert.That(b.Trace, Is.EqualTo(a.Trace));
            Assert.That(b.Fingerprint(), Is.EqualTo(a.Fingerprint()));
        }

        [Test]
        public void A_different_seed_plays_a_different_battle()
        {
            var (a, _) = Play(seed: 1u, ticks: 900);
            var (b, _) = Play(seed: 2u, ticks: 900);
            Assert.That(b.Fingerprint(), Is.Not.EqualTo(a.Fingerprint()));
        }

        [Test]
        public void A_press_in_a_frame_that_steps_no_tick_is_held_over_to_the_next_tick()
        {
            var w = new World();
            var session = new LocalSession() { Handler = w.Authority };
            w.Authority.Input = s => session.ControlsOf(s);
            // A fast frame: the dodge press arrives, but less than a tick of real time passes.
            session.Submit(new IntentCommand(session.LocalPlayer, 1, 0f, 1f, 0, 0, IntentButtons.Cancel));
            Assert.That(w.Authority.Advance(SimClock.TickSeconds * 0.4f, () => 1f), Is.EqualTo(0));
            // The next frame no longer carries the press, and completes the tick.
            session.Submit(new IntentCommand(session.LocalPlayer, 2, 0f, 1f, 0, 0, IntentButtons.None));
            Assert.That(w.Authority.Advance(SimClock.TickSeconds * 0.7f, () => 1f), Is.EqualTo(1));
            Assert.That(w.Body.IsDodging, Is.True);
        }

        [Test]
        public void The_tick_is_fixed_whatever_the_frame_rate()
        {
            var a = new GameAuthority();
            var b = new GameAuthority();
            for (var i = 0; i < 240; i++) a.Advance(1f / 240f, () => 1f);
            for (var i = 0; i < 30; i++) b.Advance(1f / 30f, () => 1f);
            Assert.That(a.Clock.Tick, Is.EqualTo(b.Clock.Tick).Within(1));
            Assert.That(a.Clock.Tick, Is.EqualTo(60).Within(1));
        }

        [Test]
        public void Battle_and_teleport_commands_round_trip_through_the_codec()
        {
            var commands = new IGameCommand[]
            {
                new BattleCommand(PlayerId.Of(1), BattleOrder.LineRush, x: 1.25f, z: -3.0000001f),
                new BattleCommand(PlayerId.Of(0), BattleOrder.Item, 3, item: "grenade"),
                new BattleCommand(PlayerId.Of(0), BattleOrder.Ability, 2, 1),
                new TeleportCommand(PlayerId.Of(0), 1.5f, 0.25f, -7f, 3.1f),
                new IntentCommand(PlayerId.Of(0), 4, 0.5f, 0f, 0, 1, IntentButtons.Cancel, 1.234567f),
            };
            foreach (var command in commands)
            {
                var line = CommandCodec.Encode(command);
                Assert.That(CommandCodec.Encode(CommandCodec.Decode(line)), Is.EqualTo(line));
            }
            Assert.That(((IntentCommand)CommandCodec.Decode("intent|0|1|0|0|0|0|0")).CameraYaw, Is.EqualTo(0f), "an older intent line still reads");
            Assert.Throws<FormatException>(() => CommandCodec.Decode("battle|0|NoSuchOrder|0|0||0|0"));
        }

        [Test]
        public void A_battle_order_without_an_open_menu_is_refused()
        {
            var w = new World();
            var session = new LocalSession() { Handler = w.Authority };
            Assert.That(w.Authority.BeginBattle(session.LocalPlayer, Setup(3u)), Is.True);
            var refused = new List<CommandRefused>();
            session.EventRaised += e => { if (e is CommandRefused r) refused.Add(r); };
            Assert.That(session.Submit(new BattleCommand(session.LocalPlayer, BattleOrder.Attack, 0)), Is.False);
            Assert.That(session.Submit(new BattleCommand(session.LocalPlayer, BattleOrder.OpenMenu)), Is.False, "the gauge starts empty");
            Assert.That(refused.Count, Is.EqualTo(2));
            Assert.That(new LocalSession().Submit(new BattleCommand(PlayerId.Of(0), BattleOrder.OpenMenu)), Is.False, "no authority attached");
        }
    }
}

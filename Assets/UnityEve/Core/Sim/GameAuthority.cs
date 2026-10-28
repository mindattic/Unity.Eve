using System;
using System.Collections.Generic;
using Unity.Eve.Core.Session;

namespace Unity.Eve.Core.Sim
{
    /// <summary>
    /// The one authority over game state (U.EVE-LAW-6): each seat's body and the battle, stepped together at
    /// the fixed tick from the seats' tick input, plus the game commands the session hands it. Presentation
    /// reads its state and listens to its events and never decides a rule. With a <see cref="Recorder"/>
    /// attached, every tick's input and time scale and every command it applied are logged, which is all a
    /// replay needs besides the battle's setup and seed.
    /// </summary>
    public sealed class GameAuthority : ICommandHandler
    {
        private readonly Dictionary<PlayerId, PlayerBody> _bodies = new Dictionary<PlayerId, PlayerBody>();
        private readonly List<PlayerId> _seats = new List<PlayerId>();
        private readonly Dictionary<PlayerId, TickInput> _inputs = new Dictionary<PlayerId, TickInput>();

        public GameAuthority()
        {
            Battle = new BattleAuthority(Clock);
            Battle.SuppressPresses = (seat, presses) => Input?.Invoke(seat)?.Suppress(presses);
        }

        public SimClock Clock { get; } = new SimClock();
        public BattleAuthority Battle { get; }
        /// <summary>Where live tick input comes from (the session's controls); null when replaying a log.</summary>
        public Func<PlayerId, PlayerControls> Input { get; set; }
        /// <summary>Logs ticks and commands while set.</summary>
        public ReplayLog Recorder { get; set; }

        /// <summary>Level geometry queries for every rule that needs them (cover).</summary>
        public IWorldQueries World
        {
            get => Battle.World;
            set
            {
                Battle.World = value ?? OpenWorld.Instance;
                foreach (var b in _bodies.Values) b.World = Battle.World;
            }
        }

        /// <summary>Raised once after each <see cref="Advance"/> that stepped at least one tick.</summary>
        public event Action Advanced;

        public IReadOnlyList<PlayerId> Seats => _seats;

        public PlayerBody BodyOf(PlayerId seat) => _bodies.TryGetValue(seat, out var b) ? b : null;

        public void Register(PlayerBody body)
        {
            if (body == null) return;
            if (!_bodies.ContainsKey(body.Seat)) _seats.Add(body.Seat);
            _bodies[body.Seat] = body;
            body.World = Battle.World;
            body.Threats = Battle;
            body.State.UseClock(Clock);
        }

        public void Unregister(PlayerBody body)
        {
            if (body == null || !_bodies.TryGetValue(body.Seat, out var b) || b != body) return;
            _bodies.Remove(body.Seat);
            _seats.Remove(body.Seat);
        }

        /// <summary>
        /// Banks <paramref name="realDeltaSeconds"/> and steps every tick now due, each at the time scale
        /// <paramref name="timeScale"/> reports when that tick starts (it can change between ticks: a perfect
        /// dodge slows the next one).
        /// </summary>
        /// <returns>Ticks stepped.</returns>
        public int Advance(float realDeltaSeconds, Func<float> timeScale)
        {
            var due = Clock.Accumulate(realDeltaSeconds);
            for (var i = 0; i < due; i++)
            {
                _inputs.Clear();
                foreach (var seat in _seats)
                {
                    var controls = Input?.Invoke(seat);
                    _inputs[seat] = controls != null ? controls.TakeTick() : TickInput.None;
                }
                Step(timeScale != null ? timeScale() : 1f, _inputs);
            }
            if (due > 0) Advanced?.Invoke();
            return due;
        }

        /// <summary>Steps exactly one tick from the given input (a replay feeds its log through here).</summary>
        public void Step(float timeScale, IReadOnlyDictionary<PlayerId, TickInput> inputs)
        {
            var dt = Clock.Begin(timeScale);
            Recorder?.RecordTick(Clock.Tick, Clock.Scale, inputs);
            foreach (var seat in _seats)
            {
                var input = inputs != null && inputs.TryGetValue(seat, out var i) ? i : TickInput.None;
                _bodies[seat].Tick(input, dt, SimClock.TickSeconds);
            }
            var battleSeat = Battle.Player != null ? Battle.Player.Seat : default;
            var battleInput = Battle.InBattle && inputs != null && inputs.TryGetValue(battleSeat, out var bi) ? bi : TickInput.None;
            Battle.Tick(battleInput, dt, SimClock.TickSeconds);
        }

        /// <summary>Starts a battle for <paramref name="seat"/> (an encounter the world triggered).</summary>
        public bool BeginBattle(PlayerId seat, BattleSetup setup)
        {
            var body = BodyOf(seat);
            if (body == null) return false;
            Recorder?.RecordBattle(Clock, seat, setup.Seed);
            return Battle.Begin(body, setup);
        }

        public string Handle(IGameCommand command)
        {
            string reason;
            switch (command)
            {
                case BattleCommand b:
                    reason = Battle.Apply(b);
                    break;
                case TeleportCommand t:
                    var body = BodyOf(t.Player);
                    if (body == null) return $"{t.Player} has no body";
                    body.Teleport(new Vec3(t.X, t.Y, t.Z), t.Yaw);
                    reason = null;
                    break;
                default:
                    return $"the authority does not take {command?.GetType().Name}";
            }
            if (reason == null) Recorder?.RecordCommand(Clock.Tick, command);
            return reason;
        }
    }
}

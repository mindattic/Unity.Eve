using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Eve.Core.Session;

namespace Unity.Eve.Core.Sim
{
    /// <summary>
    /// The replay log: everything the authority took in, as text lines in the order it took them. A battle
    /// replays from its seed plus this log: <c>battle|tick|seat|seed|now|realNow</c> where it began (the clock as it
    /// stood, so a recording made mid-session replays on a fresh authority),
    /// <c>tick|n|scale|seat|moveX|moveY|yaw|buttons[|seat|...]</c> for each fixed tick (the input each seat's tick
    /// read, presses already held over, and the tick's time scale), and <c>cmd|tick|&lt;command&gt;</c> for each
    /// command it applied (written by <see cref="CommandCodec"/>), stamped with the ticks stepped before it.
    /// </summary>
    public sealed class ReplayLog
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        public void RecordBattle(SimClock clock, PlayerId seat, uint seed) =>
            _lines.Add(string.Join("|", "battle", clock.Tick.ToString(Inv), seat.Seat.ToString(Inv), seed.ToString(Inv),
                clock.NowExact.ToString("R", Inv), clock.RealNowExact.ToString("R", Inv)));

        public void RecordTick(long tick, float scale, IReadOnlyDictionary<PlayerId, TickInput> inputs)
        {
            var parts = new List<string> { "tick", tick.ToString(Inv), scale.ToString("R", Inv) };
            if (inputs != null)
            {
                var seats = new List<PlayerId>(inputs.Keys);
                seats.Sort((a, b) => a.Seat.CompareTo(b.Seat));
                foreach (var seat in seats)
                {
                    parts.Add(seat.Seat.ToString(Inv));
                    parts.Add(inputs[seat].Encode());
                }
            }
            _lines.Add(string.Join("|", parts));
        }

        public void RecordCommand(long tick, IGameCommand command) =>
            _lines.Add("cmd|" + tick.ToString(Inv) + "|" + CommandCodec.Encode(command));

        /// <summary>
        /// Feeds <paramref name="lines"/> into <paramref name="authority"/> in order: battles begin from
        /// <paramref name="setupFor"/>(seed), commands are applied, ticks are stepped with the logged input.
        /// </summary>
        /// <exception cref="FormatException">A line this log did not write.</exception>
        /// <exception cref="InvalidOperationException">The run went somewhere the log did not (a command refused,
        /// a tick out of step): the replay diverged.</exception>
        public static void Replay(GameAuthority authority, IEnumerable<string> lines, Func<uint, BattleSetup> setupFor)
        {
            var inputs = new Dictionary<PlayerId, TickInput>();
            foreach (var line in lines)
            {
                var f = line.Split('|');
                switch (f[0])
                {
                    case "battle" when f.Length == 6:
                        if (authority.Clock.Tick == 0)
                            authority.Clock.Restore(long.Parse(f[1], Inv), double.Parse(f[4], Inv), double.Parse(f[5], Inv));
                        Expect(authority, long.Parse(f[1], Inv), line);
                        if (!authority.BeginBattle(PlayerId.Of(int.Parse(f[2], Inv)), setupFor(uint.Parse(f[3], Inv))))
                            throw new InvalidOperationException($"Replay diverged: the battle would not begin at '{line}'");
                        break;
                    case "cmd" when f.Length > 2:
                    {
                        Expect(authority, long.Parse(f[1], Inv), line);
                        var command = CommandCodec.Decode(line.Substring(f[0].Length + f[1].Length + 2));
                        var reason = authority.Handle(command);
                        if (reason != null) throw new InvalidOperationException($"Replay diverged: '{line}' was refused ({reason})");
                        break;
                    }
                    case "tick" when f.Length >= 3 && (f.Length - 3) % 5 == 0:
                    {
                        Expect(authority, long.Parse(f[1], Inv) - 1, line);
                        inputs.Clear();
                        for (var at = 3; at < f.Length; at += 5)
                            inputs[PlayerId.Of(int.Parse(f[at], Inv))] = TickInput.Decode(f, at + 1);
                        authority.Step(float.Parse(f[2], Inv), inputs);
                        break;
                    }
                    default:
                        throw new FormatException($"Unreadable replay line '{line}'");
                }
            }
        }

        private static void Expect(GameAuthority authority, long tick, string line)
        {
            if (authority.Clock.Tick != tick)
                throw new InvalidOperationException($"Replay diverged: '{line}' expected tick {tick}, the authority is at {authority.Clock.Tick}");
        }
    }
}

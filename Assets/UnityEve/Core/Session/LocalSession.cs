using System;
using System.Collections.Generic;

namespace Unity.Eve.Core.Session
{
    /// <summary>Something the session tells presentation about.</summary>
    public interface IGameEvent
    {
    }

    /// <summary>A command the session would not accept, and why.</summary>
    public sealed class CommandRefused : IGameEvent
    {
        public CommandRefused(IGameCommand command, string reason)
        {
            Command = command;
            Reason = reason;
        }

        public IGameCommand Command { get; }
        public string Reason { get; }

        public override string ToString() => $"Refused {Command}: {Reason}";
    }

    /// <summary>
    /// Applies the commands that change game state rather than a seat's controls (battle orders, scripted
    /// teleports): the authority. Returns null when the command was applied, or why it was refused.
    /// </summary>
    public interface ICommandHandler
    {
        string Handle(IGameCommand command);
    }

    /// <summary>
    /// The seam every player intent goes through (Unity.Multiplayer's Multiplayer-ready standard, rule 7).
    /// Input builds commands and submits them; the game reads each seat's controls back from the session.
    /// Single-player runs on <see cref="LocalSession"/>; an online session would implement this same interface.
    /// </summary>
    public interface ISession
    {
        /// <summary>The one answer to "which player is this machine". Rules never ask it.</summary>
        PlayerId LocalPlayer { get; }
        IReadOnlyList<PlayerId> Players { get; }
        event Action<IGameEvent> EventRaised;

        /// <returns>True when the command was accepted.</returns>
        bool Submit(IGameCommand command);

        /// <summary>The controls of a seated player, or null for a seat not in this session.</summary>
        PlayerControls ControlsOf(PlayerId player);
    }

    /// <summary>
    /// The in-process session: seats one or more players on this machine and applies each command as it is
    /// submitted, in submission order, so a frame's input reaches the game in the same frame. Controls
    /// commands update the seat's <see cref="PlayerControls"/>; game commands (<see cref="BattleCommand"/>,
    /// <see cref="TeleportCommand"/>) go to the <see cref="Handler"/>, the authority. It refuses a command from a
    /// seat that is not in the session, a stale intent (an older frame than the seat's last), a game command
    /// with no authority attached or that the authority turns down, and command types it does not know,
    /// raising <see cref="CommandRefused"/> for each.
    /// </summary>
    public sealed class LocalSession : ISession
    {
        private readonly List<PlayerId> _players = new List<PlayerId>();
        private readonly Dictionary<PlayerId, PlayerControls> _controls = new Dictionary<PlayerId, PlayerControls>();

        /// <param name="players">Seats on this machine (1 for the shipped game; more for hot-seat or tests).</param>
        /// <param name="localSeat">The seat this machine's presentation follows.</param>
        public LocalSession(int players = 1, int localSeat = 0)
        {
            if (players < 1) players = 1;
            for (var seat = 0; seat < players; seat++)
            {
                var id = PlayerId.Of(seat);
                _players.Add(id);
                _controls[id] = new PlayerControls(id);
            }
            LocalPlayer = PlayerId.Of(localSeat < 0 || localSeat >= players ? 0 : localSeat);
        }

        public PlayerId LocalPlayer { get; }
        public IReadOnlyList<PlayerId> Players => _players;
        public event Action<IGameEvent> EventRaised;

        /// <summary>Commands accepted so far.</summary>
        public long Accepted { get; private set; }

        /// <summary>Where game commands go: the authority. Null until one is attached.</summary>
        public ICommandHandler Handler { get; set; }

        public PlayerControls ControlsOf(PlayerId player) => _controls.TryGetValue(player, out var c) ? c : null;

        public bool Submit(IGameCommand command)
        {
            if (command == null) return false;
            var controls = ControlsOf(command.Player);
            if (controls == null) return Refuse(command, $"{command.Player} is not seated in this session");

            switch (command)
            {
                case IntentCommand intent:
                    if (intent.Frame < controls.Frame) return Refuse(command, $"stale frame {intent.Frame} < {controls.Frame}");
                    controls.Apply(intent);
                    break;
                case PressCommand press:
                    controls.Press(press.Button);
                    break;
                case BattleCommand _:
                case TeleportCommand _:
                    if (Handler == null) return Refuse(command, "no authority attached");
                    var reason = Handler.Handle(command);
                    if (reason != null) return Refuse(command, reason);
                    break;
                default:
                    return Refuse(command, $"unknown command {command.GetType().Name}");
            }
            Accepted++;
            return true;
        }

        private bool Refuse(IGameCommand command, string reason)
        {
            EventRaised?.Invoke(new CommandRefused(command, reason));
            return false;
        }
    }
}

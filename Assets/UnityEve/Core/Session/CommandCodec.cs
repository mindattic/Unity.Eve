using System;
using System.Globalization;

namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// Writes commands as one line of text and reads them back equal: the replay log (a seed plus the
    /// commands), a bug report that reproduces, and what an online session would send.
    /// <c>intent|seat|frame|moveX|moveY|navX|navY|buttons</c> and <c>press|seat|button</c>.
    /// </summary>
    public static class CommandCodec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Encode(IGameCommand command)
        {
            switch (command)
            {
                case IntentCommand i:
                    return string.Join("|", "intent", i.Player.Seat.ToString(Inv), i.Frame.ToString(Inv),
                        i.MoveX.ToString("R", Inv), i.MoveY.ToString("R", Inv),
                        i.NavigateX.ToString(Inv), i.NavigateY.ToString(Inv), ((int)i.Buttons).ToString(Inv));
                case PressCommand p:
                    return string.Join("|", "press", p.Player.Seat.ToString(Inv), p.Button.ToString());
                case null:
                    throw new ArgumentNullException(nameof(command));
                default:
                    throw new ArgumentException($"No encoding for {command.GetType().Name}", nameof(command));
            }
        }

        /// <exception cref="FormatException">The line is not a command this codec wrote.</exception>
        public static IGameCommand Decode(string line)
        {
            var f = (line ?? "").Split('|');
            try
            {
                switch (f[0])
                {
                    case "intent" when f.Length == 8:
                        return new IntentCommand(PlayerId.Of(int.Parse(f[1], Inv)), long.Parse(f[2], Inv),
                            float.Parse(f[3], Inv), float.Parse(f[4], Inv), int.Parse(f[5], Inv), int.Parse(f[6], Inv),
                            (IntentButtons)int.Parse(f[7], Inv));
                    case "press" when f.Length == 3:
                        return new PressCommand(PlayerId.Of(int.Parse(f[1], Inv)), (ScriptedButton)Enum.Parse(typeof(ScriptedButton), f[2]));
                }
            }
            catch (Exception e) when (!(e is FormatException))
            {
                throw new FormatException($"Unreadable command '{line}'", e);
            }
            throw new FormatException($"Unreadable command '{line}'");
        }
    }
}

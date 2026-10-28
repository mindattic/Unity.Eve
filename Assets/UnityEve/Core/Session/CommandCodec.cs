using System;
using System.Globalization;

namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// Writes commands as one line of text and reads them back equal: the replay log (a seed plus the
    /// commands), a bug report that reproduces, and what an online session would send.
    /// <c>intent|seat|frame|moveX|moveY|navX|navY|buttons|cameraYaw</c> (an older 8-field line reads with yaw 0),
    /// <c>press|seat|button</c>, <c>battle|seat|order|target|index|item|x|z</c> and <c>teleport|seat|x|y|z|yaw</c>.
    /// </summary>
    public static class CommandCodec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string F(float v) => v.ToString("R", Inv);
        private static float ParseF(string s) => float.Parse(s, NumberStyles.Float, Inv);

        public static string Encode(IGameCommand command)
        {
            switch (command)
            {
                case IntentCommand i:
                    return string.Join("|", "intent", i.Player.Seat.ToString(Inv), i.Frame.ToString(Inv),
                        F(i.MoveX), F(i.MoveY),
                        i.NavigateX.ToString(Inv), i.NavigateY.ToString(Inv), ((int)i.Buttons).ToString(Inv), F(i.CameraYaw));
                case PressCommand p:
                    return string.Join("|", "press", p.Player.Seat.ToString(Inv), p.Button.ToString());
                case BattleCommand b:
                    if (b.Item.IndexOf('|') >= 0) throw new ArgumentException("An item id cannot contain '|'", nameof(command));
                    return string.Join("|", "battle", b.Player.Seat.ToString(Inv), b.Order.ToString(), b.Target.ToString(Inv),
                        b.Index.ToString(Inv), b.Item, F(b.X), F(b.Z));
                case TeleportCommand t:
                    return string.Join("|", "teleport", t.Player.Seat.ToString(Inv), F(t.X), F(t.Y), F(t.Z), F(t.Yaw));
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
                    case "intent" when f.Length == 8 || f.Length == 9:
                        return new IntentCommand(PlayerId.Of(int.Parse(f[1], Inv)), long.Parse(f[2], Inv),
                            ParseF(f[3]), ParseF(f[4]), int.Parse(f[5], Inv), int.Parse(f[6], Inv),
                            (IntentButtons)int.Parse(f[7], Inv), f.Length == 9 ? ParseF(f[8]) : 0f);
                    case "press" when f.Length == 3:
                        return new PressCommand(PlayerId.Of(int.Parse(f[1], Inv)), Parse<ScriptedButton>(f[2]));
                    case "battle" when f.Length == 8:
                        return new BattleCommand(PlayerId.Of(int.Parse(f[1], Inv)), Parse<BattleOrder>(f[2]),
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv), f[5], ParseF(f[6]), ParseF(f[7]));
                    case "teleport" when f.Length == 6:
                        return new TeleportCommand(PlayerId.Of(int.Parse(f[1], Inv)), ParseF(f[2]), ParseF(f[3]), ParseF(f[4]), ParseF(f[5]));
                }
            }
            catch (Exception e) when (!(e is FormatException))
            {
                throw new FormatException($"Unreadable command '{line}'", e);
            }
            throw new FormatException($"Unreadable command '{line}'");
        }

        private static T Parse<T>(string name) where T : struct
        {
            if (int.TryParse(name, NumberStyles.Integer, Inv, out _) || !Enum.TryParse<T>(name, out var value) || !Enum.IsDefined(typeof(T), value))
                throw new FormatException($"Unknown {typeof(T).Name} '{name}'");
            return value;
        }
    }
}
